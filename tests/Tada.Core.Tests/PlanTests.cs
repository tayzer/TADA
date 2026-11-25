using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tada.Tests;

[TestClass]
public sealed class PlanTests
{
    [TestMethod]
    public void IndependentProtectedSuiteUsesExistingContractAndRequiresApproval()
    {
        using var project = new CoreTests.Project();
        string contract = Path.Combine(project.Root, "openapi.yaml");
        File.WriteAllText(contract, "openapi: 3.1.0\n");
        string suiteFile = WriteProtectedSuite(project, "http", "contract", ["openapi.yaml"]);
        string baseline = project.Store.PrepareProtected(suiteFile);
        var check = new PlannedCheck("http", "contract", "contract", ["OpenAPI response shape"], "Verify producer conformance.", baseline);
        var prepared = Prepare(project, Chore() with { Checks = [check] });
        Assert.AreEqual(baseline, prepared.Inventory.Checks.Single().Baseline);
        Assert.IsFalse(project.Store.Read(baseline).Files.Any(file => file.Path == "tada.json"));
        Assert.IsTrue(project.Store.Read(baseline).Files.Any(file => file.Path == "openapi.yaml"));
        Assert.ThrowsExactly<TadaException>(() => project.Store.Approved(baseline));
        project.Store.Approve(project.Store.Review(baseline).Id, "isolated protected-suite control");
        File.AppendAllText(Path.Combine(project.Root, "tada.json"), "\n");
        project.Store.CheckCurrent(baseline);
        File.AppendAllText(contract, "# unapproved change\n");
        Assert.ThrowsExactly<TadaException>(() => project.Store.CheckCurrent(baseline));
    }

    [TestMethod]
    public void ProtectedSuiteReplacementCannotDropTestsOrReclassifyAuthority()
    {
        using var project = new CoreTests.Project();
        string source = WriteProtectedSuite(project, "latency", "fitness", [], "MedianBudget", "TailBudget");
        string original = project.Store.PrepareProtected(source);
        project.Store.Approve(project.Store.Review(original).Id, "isolated protected-suite replacement control");
        string path = Path.Combine(project.Root, source);
        var definition = Data.Read<ProtectedSuiteDefinition>(path);
        File.Delete(path);
        Data.WriteNew(path, definition with { RequiredTests = ["MedianBudget"] });
        string narrowed = project.Store.PrepareProtected(source);
        Assert.ThrowsExactly<TadaException>(() => project.Store.Approve(project.Store.Review(narrowed, original).Id,
            "isolated negative control", original));
        File.Delete(path);
        Data.WriteNew(path, definition with { Kind = "contract" });
        string reclassified = project.Store.PrepareProtected(source);
        Assert.ThrowsExactly<TadaException>(() => project.Store.Approve(project.Store.Review(reclassified, original).Id,
            "isolated negative control", original));
    }

    private static string WriteProtectedSuite(CoreTests.Project project, string id, string kind, string[] roots, params string[] requiredTests)
    {
        string source = id + ".verification.json";
        Data.WriteNew(Path.Combine(project.Root, source), new ProtectedSuiteDefinition(1, id, kind,
            new Config(1, ["acceptance", .. roots], new Execution("acceptance/Acceptance.csproj", "Acceptance.dll", "native.trx"),
                ImplementationRoots: ["features"],
                Obligations: roots.Length == 0 ? null : [new ObligationRoot(kind, roots)]),
            requiredTests.Length == 0 ? ["VerifyContract"] : requiredTests));
        return source;
    }

    [TestMethod]
    public void MissingChildMetadataDoesNotHideAnotherVerifiedFailure()
    {
        using var fixture = new HistoryTests.HistoryFixture();
        string childId = fixture.Run(0, failure: true);
        string childDirectory = fixture.Directory(childId);
        string root = Path.GetFullPath(Path.Combine(childDirectory, "..", "..", ".."));
        var store = new BaselineStore(new Repository(root));
        var first = new PlannedCheck("first", "journey", "acceptance", ["REQ_ONE"], "First required check.", fixture.Baseline);
        var second = first with { Id = "second" };
        string source = Path.Combine(root, "plan.json");
        Data.WriteNew(source, Chore() with { Checks = [first, second] });
        var plan = new PlanStore(store).Prepare("plan.json");
        var child = Data.Read<RunRecord>(Path.Combine(childDirectory, "run.json"));
        string id = Guid.NewGuid().ToString("N");
        Data.WriteNew(Path.Combine(store.Repository.StateDirectory("plan-runs", id), "run.json"),
            new PlanRun(id, plan.Plan, child.Started.AddSeconds(-1), child.Finished.AddSeconds(1),
                [new CheckRun("first", childId), new CheckRun("second", Guid.NewGuid().ToString("N"))], []));
        var result = new PlanEngine(store).Verify(id);
        Assert.AreEqual("FAIL", result.Outcome);
        Assert.AreEqual("FAIL", result.Checks[0].Result.Outcome);
        Assert.AreEqual("INDETERMINATE", result.Checks[1].Result.Outcome);
    }

    [TestMethod]
    public async Task DocumentationChoreReportsNoChecksWithoutBusinessApproval()
    {
        using var project = new CoreTests.Project();
        var prepared = Prepare(project, Chore());
        var result = await new PlanEngine(project.Store).Run(prepared.Plan);
        Assert.AreEqual("NO_CHECKS", result.Outcome);
        Assert.AreEqual(0, result.Checks.Length);
        Assert.AreEqual("NO_CHECKS", new PlanEngine(project.Store).Verify(result.RunId).Outcome);
        Assert.IsFalse(Directory.Exists(Path.Combine(project.Root, ".tada", "approvals")));
    }

    [TestMethod]
    public void NoChecksNeedsAnExplicitReason()
    {
        using var project = new CoreTests.Project();
        Assert.ThrowsExactly<TadaException>(() => Prepare(project, Chore() with { NoChecksReason = null }));
    }

    [TestMethod]
    public void ChoreLabelCannotWaiveDeclaredBusinessChanges()
    {
        using var project = new CoreTests.Project();
        Assert.ThrowsExactly<TadaException>(() => Prepare(project, Chore() with { BusinessChange = true }));
    }

    [TestMethod]
    public void AcceptanceSelectionDoesNotApproveItsBaseline()
    {
        using var project = new CoreTests.Project();
        string baseline = project.Store.Prepare();
        var check = new PlannedCheck("journey", "journey", "acceptance", ["REQ_ONE"], "Verify the changed journey.", baseline);
        var prepared = Prepare(project, Chore() with { Categories = ["feat"], BusinessChange = true, Checks = [check] });
        Assert.AreEqual(baseline, prepared.Inventory.Checks.Single().Baseline);
        Assert.ThrowsExactly<TadaException>(() => project.Store.Approved(baseline));
    }

    [TestMethod]
    public async Task SupportingCaptureCannotBecomeBusinessApprovalOrDirectExecution()
    {
        using var project = new CoreTests.Project();
        var prepared = Prepare(project, Supporting(project));
        string captured = prepared.Inventory.Checks.Single().Baseline;
        var review = project.Store.Review(captured);
        Assert.ThrowsExactly<TadaException>(() => project.Store.Approve(review.Id, "isolated approval rejection control"));
        var result = await new RunEngine(project.Store).Run(captured);
        Assert.AreEqual("INDETERMINATE", result.Outcome);
        Assert.IsTrue(result.Reasons.Any(reason => reason.Detail.Contains("captured verification plan", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void SupportingEditDoesNotChangeAcceptanceAuthority()
    {
        using var project = new CoreTests.Project();
        Directory.CreateDirectory(Path.Combine(project.Root, "supporting"));
        File.WriteAllText(Path.Combine(project.Root, "supporting", "Checks.csproj"), "<Project/>");
        File.WriteAllText(Path.Combine(project.Root, "supporting", "packages.lock.json"), "{}");
        string baseline = project.Store.Prepare();
        project.Store.Approve(project.Store.Review(baseline).Id, "isolated approval boundary control");
        File.WriteAllText(Path.Combine(project.Root, "supporting", "Checks.cs"), "// revised delegated checks\n");
        project.Store.CheckCurrent(baseline);
        Assert.AreEqual(baseline, project.Store.Approved(baseline).Baseline);
        File.AppendAllText(Path.Combine(project.Root, "global.json"), "\n");
        Assert.ThrowsExactly<TadaException>(() => project.Store.CheckCurrent(baseline));
    }

    [TestMethod]
    public void PlanRevisionReportsRemovedChecksAndRequiresRationale()
    {
        using var project = new CoreTests.Project();
        var first = Prepare(project, Supporting(project));
        Assert.ThrowsExactly<TadaException>(() => Prepare(project, Chore() with { Previous = first.Plan }));
        var second = Prepare(project, Chore() with { Previous = first.Plan, ChangeReason = "The changed scope is documentation only." });
        CollectionAssert.AreEqual(new[] { "service" }, second.RemovedChecks);
    }

    [TestMethod]
    public void CapturedPlanTamperingCannotPassRead()
    {
        using var project = new CoreTests.Project();
        var prepared = Prepare(project, Chore());
        string path = Path.Combine(project.Root, ".tada", "plans", prepared.Plan[7..] + ".json");
        File.WriteAllText(path, File.ReadAllText(path).Replace("Correct a heading", "Change a business rule", StringComparison.Ordinal));
        Assert.ThrowsExactly<TadaException>(() => new PlanStore(project.Store).Read(prepared.Plan));
    }

    [TestMethod]
    public void VerifiedFailureRemainsVisibleAlongsideOtherCheckGaps()
    {
        var checks = new[]
        {
            Assessment("service", "supporting", "FAIL"),
            Assessment("journey", "acceptance", "INDETERMINATE")
        };
        var result = PlanEngine.Assess("plan", "run", checks, [], null);
        Assert.AreEqual("FAIL", result.Outcome);
        Assert.AreEqual("supporting", result.Checks[0].Purpose);
        Assert.AreEqual("INDETERMINATE", result.Checks[1].Result.Outcome);
    }

    [TestMethod]
    public void MissingCheckEvidenceBlocksAggregatePass()
    {
        Assert.AreEqual("INDETERMINATE", PlanEngine.Assess("plan", "run",
            [Assessment("service", "supporting", "PASS"), Assessment("journey", "acceptance", "INDETERMINATE")], [], null).Outcome);
    }

    [TestMethod]
    public void DuplicateServiceIdsAndMixedLegacyConfigurationAreRejected()
    {
        using var project = new CoreTests.Project();
        var config = Data.Read<Config>(Path.Combine(project.Root, "tada.json"));
        var service = new Service("api", config.Execution.Project, config.Execution.Assembly);
        File.Delete(Path.Combine(project.Root, "tada.json"));
        Data.WriteNew(Path.Combine(project.Root, "tada.json"), config with { SchemaVersion = 2, Execution = config.Execution with { Services = [service, service] } });
        Assert.ThrowsExactly<TadaException>(() => project.Store.Prepare());
    }

    [TestMethod]
    [DataRow("Passed", "PASS")]
    [DataRow("Failed", "FAIL")]
    [DataRow("NotExecuted", "INDETERMINATE")]
    public void NativeTrxOutcomesRetainTheirActualScope(string outcome, string expected)
    {
        using var project = new CoreTests.Project();
        var (path, discovery, execution) = Trx(project, outcome);
        var result = TrxVerifier.Verify(["Check"], "baseline", "run", path, discovery, execution);
        Assert.AreEqual(expected, result.Outcome, string.Join(" | ", result.Reasons.Select(reason => reason.Detail)));
        Assert.IsFalse(result.Reasons.Any(reason => reason.Code == "requirement-failure"));
    }

    [TestMethod]
    public void MissingDiscoveryAndDuplicateNativeResultsAreIndeterminate()
    {
        using var project = new CoreTests.Project();
        var (path, discovery, execution) = Trx(project, "Passed");
        Assert.AreEqual("INDETERMINATE", TrxVerifier.Verify(["Absent"], "baseline", "run", path, discovery, execution).Outcome);
        var document = XDocument.Load(path);
        var results = document.Root!.Elements().First(element => element.Name.LocalName == "Results");
        results.Add(new XElement(results.Elements().Single()));
        document.Save(path);
        Assert.AreEqual("INDETERMINATE", TrxVerifier.Verify(["Check"], "baseline", "run", path, discovery, execution).Outcome);
    }

    [TestMethod]
    public void RunnerFailureCannotBeHiddenByPassingTrx()
    {
        using var project = new CoreTests.Project();
        var (path, discovery, execution) = Trx(project, "Passed");
        Assert.AreEqual("INDETERMINATE", TrxVerifier.Verify(["Check"], "baseline", "run", path, discovery, execution with { ExitCode = 2 }).Outcome);
    }

    [TestMethod]
    public void ProtectedTrxRequiresExactTestSetAndIdentifiedAssertionFailures()
    {
        using var project = new CoreTests.Project();
        var (path, discovery, execution) = Trx(project, "Passed");
        var unexpected = discovery with { Stdout = "  Check\n  Other\nTest discovery summary: found 2 test(s) - Checks.dll\n" };
        Assert.AreEqual("protected-evidence", TrxVerifier.Verify(["Check"], "baseline", "run", path, unexpected, execution, true).Reasons.Single().Code);

        (path, discovery, execution) = Trx(project, "NotExecuted");
        Assert.AreEqual("INDETERMINATE", TrxVerifier.Verify(["Check"], "baseline", "run", path, discovery, execution, true).Outcome);

        (path, discovery, execution) = Trx(project, "Failed");
        Assert.AreEqual("INDETERMINATE", TrxVerifier.Verify(["Check"], "baseline", "run", path, discovery, execution, true).Outcome);
        var document = XDocument.Load(path);
        XNamespace ns = document.Root!.Name.Namespace;
        document.Descendants(ns + "UnitTestResult").Single().Add(
            new XElement(ns + "Output", new XElement(ns + "ErrorInfo", new XElement(ns + "Message", "Assert.IsTrue failed. Required threshold exceeded."))));
        document.Save(path);
        Assert.AreEqual("FAIL", TrxVerifier.Verify(["Check"], "baseline", "run", path, discovery, execution, true).Outcome);
        Assert.AreEqual("INDETERMINATE", TrxVerifier.Verify(["Check"], "baseline", "run", path, discovery, execution with { ExitCode = 1 }, true).Outcome);
        document.Descendants(ns + "Message").Single().Value = "Assertion failed. Expected value to be within the allocation budget.";
        document.Save(path);
        Assert.AreEqual("FAIL", TrxVerifier.Verify(["Check"], "baseline", "run", path, discovery, execution, true).Outcome);
    }

    private static CheckAssessment Assessment(string id, string purpose, string outcome) => new(id, id, purpose,
        new Verification(outcome, "L1-advisory", "baseline", "run", [], []));

    private static VerificationPlan Chore() => new(1, "Correct a heading", ["chore"], false, ["README heading"], [], [], "Only a heading changes. Review the diff.");

    private static VerificationPlan Supporting(CoreTests.Project project)
    {
        var config = Data.Read<Config>(Path.Combine(project.Root, "tada.json"));
        var engineering = new Engineering(config with { Execution = config.Execution with { MessagesFile = "supporting.trx" } }, ["Check"]);
        return Chore() with { Checks = [new PlannedCheck("service", "service", "supporting", ["Preserve API behaviour"], "Existing service regression coverage.", Engineering: engineering)] };
    }

    private static PlanPreparation Prepare(CoreTests.Project project, VerificationPlan plan)
    {
        string file = Path.Combine(project.Root, "plan.json");
        if (File.Exists(file)) File.Delete(file);
        Data.WriteNew(file, plan);
        return new PlanStore(project.Store).Prepare("plan.json");
    }

    private static (string, CommandEvidence, CommandEvidence) Trx(CoreTests.Project project, string outcome)
    {
        string path = Path.Combine(project.Root, "supporting.trx");
        var now = DateTimeOffset.UtcNow;
        string assembly = Path.Combine(project.Root, "Checks.dll");
        File.WriteAllText(path, $"""
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <Times start="{now:O}" finish="{now.AddMilliseconds(500):O}" />
              <Results><UnitTestResult testName="Check" testId="test" executionId="one" startTime="{now:O}" endTime="{now.AddMilliseconds(500):O}" outcome="{outcome}" /></Results>
              <TestDefinitions><UnitTest name="Check" id="test"><Execution id="one" /><TestMethod codeBase="{System.Security.SecurityElement.Escape(assembly)}" adapterTypeName="executor://MSTest.Sdk/4.5.2" /></UnitTest></TestDefinitions>
              <TestEntries><TestEntry testId="test" executionId="one" /></TestEntries>
              <ResultSummary outcome="{(outcome == "Failed" ? "Failed" : "Completed")}"><Counters total="1" executed="1" passed="{(outcome == "Passed" ? 1 : 0)}" failed="{(outcome == "Failed" ? 1 : 0)}" /></ResultSummary>
            </TestRun>
            """);
        return (path, new CommandEvidence("dotnet", [assembly, "--list-tests"], 0, false, now.AddSeconds(-2), now.AddSeconds(-1), "  Check\n\nTest discovery summary: found 1 test(s) - Checks.dll\n  duration: 10ms\n", ""),
            new CommandEvidence("dotnet", [assembly, "--report-trx"], outcome == "Failed" ? 2 : 0, false, now, now.AddSeconds(1), "", ""));
    }
}
