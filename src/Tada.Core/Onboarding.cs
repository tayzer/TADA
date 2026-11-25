namespace Tada;

public static class Onboarding
{
    public sealed record Initialisation(string[] Created, string[] Updated, string Layout, string[] Discovered);

    private const string LayoutPath = ".tada/layout.json";
    private const string Functional = "specifications/functional";
    private const string Acceptance = "tests/Acceptance";
    private sealed record LayoutPreference(int SchemaVersion, string Layout, bool Greenfield);

    public static Initialisation Initialise(string root, string? dotnetHost = null, string? layout = null, bool acceptance = false)
    {
        if (layout is not (null or "existing" or "recommended"))
            throw new TadaException("Choose --layout existing or --layout recommended.");
        Directory.CreateDirectory(root);
        var repository = new Repository(root);
        root = repository.Root;
        var discovered = Discover(repository);
        string preferencePath = Path.Combine(root, LayoutPath);
        var prior = File.Exists(preferencePath) ? Data.Read<LayoutPreference>(repository.Resolve(LayoutPath)) : null;
        if (prior != null && (prior.SchemaVersion != 1 || prior.Layout is not ("existing" or "recommended")))
            throw new TadaException($"Unsupported layout preference: {LayoutPath}");
        string config = Path.Combine(root, "tada.json");
        bool configured = File.Exists(config);
        bool greenfield = prior?.Greenfield ?? (!configured && discovered.Length == 0);
        var preference = new LayoutPreference(1, layout ?? prior?.Layout ?? (greenfield ? "recommended" : "existing"), greenfield);
        var files = new Dictionary<string, string>
        {
            [".tada/WORKFLOW.md"] = Workflow,
            [".tada/context.md"] = "# Project context\n\nCurrent-work handoff for the next session: objective and branch, authoritative links, implementation and verification state, open questions, next action and links to durable knowledge. Replace finished work instead of appending history; lasting facts belong in their canonical documents. This is not a functional specification. No work is in progress yet.\n"
                + (dotnetHost == null ? "" : $"\n## Local SDK host\n\nInitialisation used `{dotnetHost}`. Recheck this path and the stable .NET 10.0 SDK selected by this repository's `global.json` before use, especially after moving machines. Use this host for SDK/tool commands and pass the same host through `--dotnet` to `tada run`. TADA records the resolved SDK and observed toolchain identities for each run. This is an environment note, not business authority.\n"),
            [".tada/.gitattributes"] = "# Captured approval/evidence bytes must survive every checkout unchanged.\n* -text",
            [".tada/.gitignore"] = "/runs/\n/plan-runs/\n/scratch/"
        };
        if (greenfield)
        {
            files.Add(".gitattributes", "# Approval identities use exact file bytes. Keep Git from rewriting line endings.\n* -text");
            files.Add(".gitignore", "**/bin/\n**/obj/");
        }

        if (acceptance && !configured)
        {
            if (preference.Layout != "recommended")
                throw new TadaException("Scaffolding uses recommended directories. Use --layout recommended or configure the existing harness in tada.json.");
            var harness = AcceptanceFiles();
            string directory = Path.Combine(root, Acceptance);
            if (Directory.Exists(directory))
            {
                repository.Resolve(Acceptance, directory: true);
                if (Directory.EnumerateFileSystemEntries(directory).Any(item =>
                    !harness.TryGetValue(repository.Relative(item), out var template) ||
                    File.ReadAllText(repository.Resolve(repository.Relative(item))) != template + "\n"))
                    throw new TadaException($"{Acceptance} already contains verification infrastructure. Configure that harness in tada.json or choose a different directory manually.");
            }
            foreach (var entry in harness) files.Add(entry.Key, entry.Value);
            if (greenfield)
            {
                files.Add("global.json", "{\"sdk\":{\"version\":\"10.0.100\",\"rollForward\":\"latestFeature\",\"allowPrerelease\":false},\"test\":{\"runner\":\"Microsoft.Testing.Platform\"}}");
                files.Add("NuGet.Config", "<configuration><packageSources><clear/><add key=\"nuget.org\" value=\"https://api.nuget.org/v3/index.json\"/></packageSources></configuration>");
                files.Add("Directory.Build.props", "<Project><PropertyGroup><RestorePackagesWithLockFile>true</RestorePackagesWithLockFile></PropertyGroup></Project>");
            }
        }
        var created = new List<string>();
        var updated = new List<string>();
        // Publish the layout before optional scaffolding so a retry retains the original selection.
        if (prior != preference)
        {
            string stage = Path.Combine(repository.StateDirectory("scratch"), Guid.NewGuid().ToString("N") + ".json");
            try
            {
                Data.WriteNew(stage, preference);
                File.Move(stage, preferencePath, overwrite: prior != null);
            }
            finally { if (File.Exists(stage)) File.Delete(stage); }
            (prior == null ? created : updated).Add(LayoutPath);
        }
        foreach (var (relative, content) in files)
        {
            string path = Path.Combine(root, relative);
            if (File.Exists(path)) continue;
            int separator = relative.LastIndexOf('/');
            if (separator >= 0) EnsureDirectory(relative[..separator]);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)))
                writer.Write(content + "\n");
            created.Add(relative);
        }
        if (acceptance && !configured)
        {
            EnsureDirectory(Functional);
            Data.WriteNew(config, new Config(1, [Functional, Acceptance],
                new Execution(Acceptance + "/Acceptance.csproj", "Acceptance.dll", "tada-messages.ndjson")));
            created.Add("tada.json");
        }
        foreach (string entry in new[] { "AGENTS.md", "AGENTS.override.md" })
            if (File.Exists(Path.Combine(root, entry)) && RemoveLegacyGuidance(entry)) updated.Add(entry);
        return new Initialisation(created.ToArray(), updated.ToArray(), preference.Layout, discovered);

        void EnsureDirectory(string relative)
        {
            string current = root;
            foreach (string component in relative.Split('/'))
            {
                current = Path.Combine(current, component);
                if (!Directory.Exists(current)) Directory.CreateDirectory(current);
                Repository.NoLink(current);
            }
        }

        bool RemoveLegacyGuidance(string relative)
        {
            string path = repository.Resolve(relative);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            using var reader = new StreamReader(stream, new System.Text.UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            string existing;
            try { existing = reader.ReadToEnd(); }
            catch (System.Text.DecoderFallbackException)
            {
                throw new TadaException($"Cannot decode {relative} as UTF-8 or BOM-marked Unicode. Existing instructions were left unchanged.");
            }
            var sections = new List<string>();
            if (existing.Contains("<!-- TADA:BEGIN -->", StringComparison.Ordinal) || existing.Contains("<!-- TADA:END -->", StringComparison.Ordinal))
            {
                int start = existing.IndexOf(AgentInstructions, StringComparison.Ordinal);
                if (start < 0 || existing.IndexOf("<!-- TADA:BEGIN -->", StringComparison.Ordinal) != start ||
                    existing.LastIndexOf("<!-- TADA:BEGIN -->", StringComparison.Ordinal) != start ||
                    existing.IndexOf("<!-- TADA:END -->", StringComparison.Ordinal) != start + AgentInstructions.Length - "<!-- TADA:END -->".Length ||
                    existing.LastIndexOf("<!-- TADA:END -->", StringComparison.Ordinal) != start + AgentInstructions.Length - "<!-- TADA:END -->".Length)
                    throw new TadaException($"Edited or incomplete legacy TADA guidance in {relative} requires reconciliation before plugin controls can govern the workflow.");
                string owned = AgentInstructions;
                if (start >= 2 && existing.AsSpan(start - 2, 2).SequenceEqual("\n\n")) owned = "\n\n" + owned;
                if (existing.AsSpan(start + AgentInstructions.Length).StartsWith("\n")) owned += "\n";
                sections.Add(owned);
            }
            if (existing.Contains(LegacyEntryParagraph, StringComparison.Ordinal)) sections.Add(LegacyEntryParagraph);
            if (sections.Count == 0) return false;
            // Preserve the BOM, encoding and every byte outside known generated guidance.
            stream.Position = 0;
            byte[] remaining = new byte[checked((int)stream.Length)];
            stream.ReadExactly(remaining);
            byte[] original = remaining;
            foreach (string section in sections)
            {
                byte[] encoded = reader.CurrentEncoding.GetBytes(section);
                int offset = remaining.AsSpan().IndexOf(encoded);
                if (offset < 0) throw new TadaException($"Cannot locate the legacy guidance bytes in {relative}.");
                remaining = [.. remaining.AsSpan(0, offset), .. remaining.AsSpan(offset + encoded.Length)];
            }
            string temporary = path + ".tada-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    output.Write(remaining);
                    output.Flush(flushToDisk: true);
                }
                reader.Dispose();
                stream.Dispose();
                if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(original))
                    throw new TadaException($"Instructions changed while migrating {relative}. Retry against the current file.");
                File.Move(temporary, path, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return true;
        }
    }

    private static Dictionary<string, string> AcceptanceFiles() => new()
    {
        ["tests/Acceptance/Acceptance.csproj"] = """
            <Project Sdk="MSTest.Sdk/4.5.2">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType>
                <ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>
                <EnableMSTestRunner>true</EnableMSTestRunner><UseVSTest>false</UseVSTest>
                <TestingExtensionsProfile>None</TestingExtensionsProfile>
                <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
                <ReqnrollUseIntermediateOutputPathForCodeBehind>true</ReqnrollUseIntermediateOutputPathForCodeBehind>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Reqnroll.MSTest" Version="[3.3.4]"/>
                <PackageReference Include="Reqnroll" Version="[3.3.4]"/>
                <PackageReference Include="Reqnroll.Tools.MsBuild.Generation" Version="[3.3.4]"/>
                <None Update="reqnroll.json" CopyToOutputDirectory="PreserveNewest"/>
                <ReqnrollFeatureFile Include="../../specifications/functional/**/*.feature" Link="Features/%(RecursiveDir)%(Filename)%(Extension)"/>
              </ItemGroup>
              <Import Project="Tada.Acceptance.targets" />
            </Project>
            """,
        ["tests/Acceptance/reqnroll.json"] = "{\"formatters\":{\"message\":{\"outputFilePath\":\"tada-messages.ndjson\"}}}\n",
        ["tests/Acceptance/TadaRuntimeCheckpoint.cs"] = RuntimeCheckpoint,
        ["tests/Acceptance/TadaAcceptance.cs"] = Template("TadaAcceptance.cs.txt"),
        ["tests/Acceptance/Tada.Acceptance.targets"] = Template("Tada.Acceptance.targets")
    };

    private static string[] Discover(Repository repository)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        var directories = new Stack<string>([repository.Root]);
        while (directories.TryPop(out string? directory))
        {
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(path);
                if (attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                string name = Path.GetFileName(path).ToLowerInvariant();
                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    if (!name.StartsWith('.') && name is not ("bin" or "obj" or "node_modules" or "vendor" or "dist" or "target" or "testresults"))
                        directories.Push(path);
                    continue;
                }
                string relative = repository.Relative(path);
                string extension = Path.GetExtension(name);
                string[] segments = relative.Split('/');
                if (extension is ".feature" or ".csproj" or ".fsproj" or ".vbproj" or ".sln" or ".slnx" ||
                    name is "tada.json" or "reqnroll.json" || name.EndsWith(".verification.json", StringComparison.Ordinal) ||
                    name.StartsWith("openapi", StringComparison.Ordinal) || name.StartsWith("swagger", StringComparison.Ordinal) ||
                    (!name.StartsWith("readme", StringComparison.Ordinal) && segments.SkipLast(1).Any(segment => segment.ToLowerInvariant() is
                        "features" or "spec" or "specs" or "specifications" or "contracts" or "interfaces")))
                    found.Add(relative);
                else if (extension is ".cs" or ".py" or ".go" or ".rs" or ".java" or ".ts" or ".tsx" or ".js" or ".jsx" or ".php" or ".swift" or ".kt" ||
                    (!name.StartsWith("readme", StringComparison.Ordinal) && segments.SkipLast(1).Any(segment => segment.ToLowerInvariant() is
                        "test" or "tests" or "checks" or "fitness" or "src")))
                    found.Add(segments.Length == 1 ? relative : segments[0] + "/");
            }
        }
        return found.Order(StringComparer.Ordinal).ToArray();
    }

    private const string LegacyEntryParagraph = "Read `.tada/WORKFLOW.md` and `.tada/context.md` before changing this repository. Gherkin defines business acceptance. Ask the human to agree acceptance-changing decisions; routine implementation choices are delegated.\n";

    // Exact alpha.8 bytes form the migration signature.
    public const string AgentInstructions = """
        <!-- TADA:BEGIN -->
        ## TADA is enabled for this repository

        Apply the TADA workflow by default to ordinary project requests. The user does not need to mention TADA, Gherkin, tests or this file in their prompt. Before acting, read `.tada/WORKFLOW.md` and `.tada/context.md`, recover the current goal and evidence, and inspect applicable project instructions. Respect explicit user decisions and flag conflicting instructions rather than silently bypassing them.

        - For a new project or feature, discover the intended behaviour with the human, agree success and failure examples, and express agreed acceptance in executable Gherkin before implementing business behaviour. Prepare meaningful independent bindings and the immutable baseline for human review/approval. Never approve expectations on the human's behalf. Implement in small verified increments; distinguish business RED from broken setup.
        - For a bug fix, use an existing agreed executable scenario when it describes the defect; otherwise agree the missing expectation. Demonstrate the intended regression, repair it and recheck existing obligations. Never weaken assertions or erase failure history to obtain PASS.
        - For a chore or behaviour-preserving refactor, choose proportionate existing regression, build or quality checks without inventing business scenarios. Return to acceptance discussion if behaviour changes.
        - Use the SDK/tool host recorded in context when applicable; recheck its availability and the pinned SDK. Run the repository-local tool through that host (`dotnet tool run tada`), and pass the same host through `--dotnet` when needed. Keep environment setup details out of the user's project request.
        - Before claiming completion, verify the selected agreed expectations and useful quality checks, report unresolved gaps and local advisory L1 limits, and update concise Git-native context with evidence and the next action. A fresh session must recover this itself; do not ask the user to repeat the workflow.
        <!-- TADA:END -->
        """;

    private static string Template(string name)
    {
        using var stream = typeof(Onboarding).Assembly.GetManifestResourceStream("Tada.Templates." + name)
            ?? throw new InvalidOperationException("Missing acceptance template: " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public const string RuntimeCheckpoint = """
        using System.Diagnostics;

        // Cooperate with TADA's injected observer without referencing application or verifier assemblies.
        public static class TadaRuntimeCheckpoint
        {
            public static void Complete(Process process)
            {
                string? directory = Environment.GetEnvironmentVariable("TADA_RUNTIME_DIRECTORY");
                if (directory == null || process.HasExited) return;
                string run = Environment.GetEnvironmentVariable("TADA_RUN_ID") ?? throw new InvalidOperationException("TADA run identity is missing.");
                string acknowledgement = Path.Combine(directory, $"{process.Id}.checkpoint-complete");
                File.WriteAllText(Path.Combine(directory, $"{process.Id}.checkpoint-request"), run);
                var deadline = Stopwatch.StartNew();
                while (true)
                {
                    try
                    {
                        if (File.Exists(acknowledgement))
                        {
                            if (File.ReadAllText(acknowledgement) != run) throw new IOException("API runtime checkpoint belongs to another run.");
                            return;
                        }
                    }
                    catch (IOException error) when ((error.HResult & 65535) is 32 or 33)
                    {
                        // A transient Windows sharing conflict still counts toward the checkpoint deadline.
                    }
                    if (process.HasExited) throw new IOException("API exited before its runtime checkpoint completed.");
                    if (deadline.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException("API runtime checkpoint did not complete.");
                    Thread.Sleep(10);
                }
            }
        }
        """;

    public const string Workflow = """
        # TADA agent workflow

        Apply this workflow when the TADA plugin is enabled or the user explicitly requests it. The plugin supplies automatic workflow selection for ordinary software requests. Read this file and `context.md` when applying TADA to recover project knowledge. The host's plugin controls govern automatic selection in fresh sessions.

        ## Understand before agreeing

        Separate the requested outcome from any suggested implementation. Investigate in proportion to impact and risk: low for documentation, bounded chores and contained refactors (relevant files, conventions, existing checks); medium for business fixes and bounded features (behavioural paths, related scenarios and contracts, persistent state, regression history); high for cross-service, consequential data, security-sensitive and distributed work (service and data ownership, asynchronous consumers, retries and idempotency, consistency, failure recovery, authorisation, NFRs). These are guides, not gates. Stop when you can responsibly propose the affected acceptance expectations, and report unreachable sources as limitations.

        Retrieve knowledge by relevance: features, contract and fitness roots, accepted decisions and project documents, `.tada/knowledge.md`, matching `.tada/knowledge/` records and the Git history of touched paths. Treat `context.md`, records and memory as leads; check each fact you rely on against current code and accepted decisions. Report conflicts between sources rather than choosing one.

        Before agreeing new acceptance, share a change impact brief scaled to the change: Verified findings with references, Inferred conclusions, Unknowns and who decides, relevant history, functional and non-functional requirements and verification implications. It is conversation, not a specification or a required file. Code shows current behaviour, not intended behaviour. Never turn an inference into an agreed requirement.

        Authority, highest first: approved Gherkin, contracts and fitness obligations; accepted decisions and established project documentation; current code, configuration, tests and evidence (what the system does now); reviewed knowledge records, issues, pull requests and commits (historical findings); proposed records, the context handoff and inferences (leads). Superseded material is history only.

        ## Choose proportionate work

        - Feature or business defect: discover examples with the human. Put agreed behaviour in Gherkin, not a competing Markdown specification. Tag explicit requirement IDs `@REQ_...` and each ordinary scenario `@AC_...`. Resolve ambiguity that changes acceptance. Do not approve on the human's behalf.
        - Chore, maintenance or behaviour-preserving refactor: state the objective and choose appropriate existing regression, build, format, compatibility or fitness checks. Do not invent a business scenario for ceremony. Reclassify if behaviour changes. Existing acceptance obligations still apply.
        - Quality goals need concrete workloads/thresholds or explicit human review criteria; undefined targets cannot yield a green claim. Discover non-functional requirements with the behaviour, not afterwards.
        - Reuse existing authoritative contracts and executable thresholds. Classify their protected roots under `obligations`. Use `tada baseline prepare --file <suite-file>` for an independent protected MSTest project. Supporting engineering tests can remain outside the protected oracle.

        During initial discovery, record actual change categories (`feat`, `fix`, `refactor`, `chore`, including mixtures), objective, impacted services/interfaces/storage/journeys, existing coverage, gaps and selected verification levels. Branch labels are hints. Unit, service, producer/consumer contract and journey checks have distinct scopes. Choose levels by impact and risk and explain omissions. Refactors start with preservation checks and need characterisation when existing coverage is inadequate. Chores can require no test execution with an explicit reason and an appropriate evaluation of success. Human approval covers business expectations and executable acceptance. Supporting engineering checks are delegated.

        Keep lightweight selections and results in context when existing tools suffice. Coordinated execution can use `.tada/verification-plan.json`, captured with `tada plan prepare --file .tada/verification-plan.json`, executed with `tada run --plan <digest>` and verified with `tada verify --run <id>`. The plan has schemaVersion 1, objective, categories, businessChange, impact, checks and omissions. Every check has a unique lowercase id, level (`unit`, `service`, `contract`, `journey`, `fitness`), purpose (`acceptance`, `supporting`, `contract`, `fitness`), obligations and rationale. Acceptance checks reference a business baseline. Independent contract and fitness checks reference an approved native baseline and use its stable ID. Supporting checks contain engineering.configuration (the normal TADA configuration) and engineering.requiredTests (exact unique names printed by native MTP discovery). Their messagesFile is a `.trx` filename. EnableMicrosoftTestingExtensionsTrxReport must be true in the supporting MSTest project and its restored graph must be locked. Execution reconciles native discovery with every reported case and runs each selected project once. Selecting no checks needs noChecksReason and reports NO_CHECKS with exit 0, without a passing-test claim. Revisions identify previous and changeReason. Supporting snapshots cannot receive business approval. Changing shared acceptance inputs still invalidates the business baseline.

        For multiple local services, schema-2 tada.json replaces the legacy SUT pair with execution.services, each containing id, project and assembly. Match native build-only ProjectReferences with TadaServiceId. Access staged or captured assemblies using TadaAcceptance.Create().Services[id]. Each scenario owns readiness, ports, state, correlation and cleanup. Checkpoint every service before termination. Every configured service needs complete runtime observations. Ordinary supporting unit tests may reference selected implementation assemblies. Business acceptance retains its independent-assertion boundary. Bounded asynchronous assertions must distinguish an observed unmet deadline from unavailable observation. Results identify the exact local service combination and retain advisory L1 limits.

        ## Specify, verify, proceed

        1. Inspect the current code, relevant Gherkin, context and Git status. Preserve unrelated work.
        2. Agree examples and boundaries with the human. For a new API, choose one useful endpoint first; identify observable success and failure cases before coding the application.
        3. Build scenario-scoped bindings, setup, public observations and meaningful assertions. Keep the harness independent of application implementation assemblies. Review privileged observations explicitly. Each scenario owns its resources and attempts cleanup after failure.

        Keep generated databases, state and logs under the supplied `TADA_RUN_DIRECTORY`, outside immutable build outputs. Static application/harness outputs, including non-code resources, are integrity checked. The configured native Messages file and MTP's reserved `TestResults` diagnostics directory are runtime-evidence exceptions, not locations for expected assertion data.

        Acceptance bindings should use the generated `TadaAcceptance.Create()` context. Its `WorkingDirectory`, `SutAssembly` and `DotnetHost` support both ordinary MSTest/MTP execution and recorded TADA runs. Native invocation creates isolated temporary storage and uses the staged API. Add a build-only API `ProjectReference` with `ReferenceOutputAssembly="false"`, `Private="false"` and `OutputItemType="TadaSutOutput"`; match it to `execution.sutProject`. Import the generated `Tada.Acceptance.targets`. The SDK builds the API dependency, and the target stages its complete runtime output under `sut/` beside the harness. Visual Studio Test Explorer and `dotnet test --project tests/Acceptance/Acceptance.csproj` can then run the same scenarios without a wrapper or manual run variables. Broken or partial TADA run context remains a setup error.

        Native results appear in the normal test runner. Native Messages and scenario logs use an isolated temporary directory printed in test output. Business fixtures own API termination and database cleanup, retaining useful diagnostics. TADA assessment additionally requires the approved baseline, captured execution and `tada verify`.
        4. Run `dotnet restore tests/Acceptance/Acceptance.csproj` to create a dependency lock. Pin and review build inputs before baseline preparation. Configure the harness and optional SUT build project in `tada.json`.

        Include linked source, shared project references and custom imports in the appropriate selected oracle or implementation roots. Each project needs its own dependency lock. TADA uses fresh build intermediates and rejects observed inputs outside those selections or the supported installed SDK/pinned-package policy. This local policy trusts reviewed build targets and installed toolchain/cache contents; it is not an execution sandbox.

        TADA runs builds and the harness with a recorded environment containing supported OS locations and .NET/NuGet cache locations. Arbitrary ambient MSBuild properties, CLR injection variables and automatic MSBuild response files are excluded. Set scenario-specific application inputs explicitly in reviewed bindings.

        Actual restore/rebuild task journals identify invoked code and structured file inputs. SDK/pinned-package tasks remain trusted toolchain components. Standard file-reading tasks must consume captured inputs. Opaque custom tasks, shell commands and file/opaque project-evaluation expressions require exact `buildTaskContracts` declarations in `tada.json`: `origin`, `target`, `task` and `inputRoots`. The origin and declared roots join the immutable oracle snapshot. Use target `@evaluation` and task `property-functions` for opaque evaluation. Review the code and declare every read root; do not add broad trust entries merely to suppress a gap. This is an explicit local trust contract, not file-system tracing or a sandbox.
        5. Run `tada baseline prepare`. Review its printed digest using `tada baseline review --baseline <digest>`. Review captured source in `.tada/snapshots/<hex>/`, not only the inventory summary. For amendments, pass `--against <old-digest>`.
        6. The human records explicit approval with `tada baseline approve --review <review-id> --by <name>`. Approval defines the target; it does not require green tests. Use `--replaces <old-digest>` for a reviewed replacement that preserves earlier obligations.

        Record enforcement status in `.tada/enforcement.json`: `{"schemaVersion":1,"baseline":"<digest>","active":[...],"candidates":[{"scenario":"AC_...","since":"yyyy-MM-dd","reason":"..."}]}`. Implemented scenarios are `active` and must pass for merge. Approved scenarios awaiting implementation are visible `candidates`; a spec-only change may merge them. Never demote an active scenario or reset a candidate's `since` date. An optional `"policy":{"candidateMaxDays":30}` blocks overdue candidates.
        7. Run `tada run --baseline <digest>`. A defect should fail at its intended outcome assertion. Missing bindings, a broken build or unavailable setup are INDETERMINATE, not meaningful behavioural RED. Already-satisfied behaviour needs no artificial RED.
        8. Implement a small change to the application. Preserve the approved oracle. Repeat the run; resolve failures and evidence gaps. Never remove assertions, silently change baseline content, filter away required cases or discard failure history to obtain PASS.
        9. Run appropriate regression and quality checks. Use `tada verify --run <id>` to inspect retained evidence without executing the application again. Run `tada gate --run <id>`, adding `--base <target-branch>` when one exists: active obligations must pass, candidates are reported, and a passing candidate must be moved to `active` in the same change. `tada gate` without `--run` reports current status for a new session. PASS is scoped to the selected approved expectations, not complete correctness.
        10. Record a concise handoff in `.tada/context.md`: current goal and branch, authoritative feature links, baseline and run IDs, pending candidates, implementation identity, commands/results, unresolved issues, the next action and links to durable knowledge. Replace finished work rather than appending summaries; say so when no work is in progress. Review and commit useful context, approvals and baseline records using normal Git review. Do not commit secrets or ephemeral runtime state.
        11. Preserve learning selectively. Keep a finding only if it would influence future decisions, prevent a recurring defect, explain a trade-off, reveal a non-obvious constraint or save repeated investigation. Prefer its canonical home: features, contract or fitness roots, the project's ADR convention, existing documents, or the issue or pull request. `.tada/knowledge.json` selects the creation policy: `{"schemaVersion":1,"mode":"reference"}` (the default when absent) or `"augment"`, which only a human chooses. Reference mode reuses and improves existing knowledge, maintains the `.tada/knowledge.md` pointer index and never creates standalone records; report significant gaps instead. Apply this at the end of every session, including paused work. Augment mode may also propose one focused record in `.tada/knowledge/<topic>.md` for a meaningful gap without a suitable home. Records start with front matter (`status: proposed`, `kind` of discovery, defect, invariant, pitfall, constraint or trade-off, `checked: <commit>`, `sources: <repository paths>`) and separate Verified, Inferred and Unknown claims. Humans review them through Git and may set `status: reviewed`; an agent that changes a reviewed record's claims sets it back to `proposed`; replacements use `status: superseded` with `supersededBy`. Records are supporting context and never define acceptance. `tada knowledge check [--base <target-branch>]` reports the mode, broken links, records whose sources changed since `checked`, and records reference mode does not allow. The mode file is outside the captured oracle, so changing it does not invalidate approved baselines.

        ## Results and trust

        PASS=0: complete supported evidence satisfies the approved cases. FAIL=1: a trustworthy outcome assertion demonstrates unmet behaviour. INDETERMINATE=2: integrity, infrastructure or evidence gaps prevent verification. Command error=3. The gate also reports NO_CHECKS=0 when nothing is enforced and nothing ran; that is not a behavioural PASS.

        The gate lists protected changes against the base branch by kind: specification, executable interpretation, contract, fitness, verification configuration and enforcement. Each needs human review. In team CI, local approval records are declarations, so require independent review of these changes.

        Results are local advisory L1. A controlling local actor can forge approval or producer records. Exact hashes detect correspondence, not assertion adequacy or authentic human review. Running the application separately does not establish protected independent assurance.

        Build-input evidence identifies supported evaluated compiler/resource/reference items and preprocessed imports. Runtime observation identifies located managed assemblies, native loader events and initial/final module snapshots against captured outputs, the pre-run .NET platform and trusted Windows system locations. Native loader events retain paths even when a DLL is unloaded before polling; lost/incomplete events block PASS. Arbitrary custom-task file reads remain untraced. Preserve these limitations in reports; do not claim complete execution-input coverage.

        Before terminating each scenario-owned API, call the generated `TadaRuntimeCheckpoint.Complete(api)` helper. Put process termination, waiting and disposal in `finally` so failed observation still releases resources. The helper requests a final managed/native snapshot and waits for its acknowledgement. Normal graceful exits are captured automatically. An abruptly killed API without a final snapshot is INDETERMINATE, even when the native test runner reports success. API application code needs no TADA dependency.

        ## Bootstrap and maintenance

        `tada init` records the invoking SDK host in new context and saves the layout in `.tada/layout.json`. Inspect existing project files, oracle roots and CI. New projects use `specifications/functional/`, `specifications/non-functional/`, `specifications/contracts/`, `src/` and the appropriate projects under `tests/`. Existing projects reuse established conventions. `--layout recommended` opts into recommended paths for future work. Create only needed infrastructure. `--acceptance` scaffolds the supported .NET harness in a vacant `tests/Acceptance/`; configure existing harnesses in place. Enable or disable the TADA plugin through the host's plugin controls. Initialisation removes the exact legacy alpha.8 TADA block from root agent instructions so the plugin toggle can govern automatic behaviour. Edited legacy blocks require reconciliation. Existing baseline, review, approval and run records remain project knowledge for future work. Scenario Outlines are deliberately unsupported in the initial version.

        New projects disable Git line-ending conversion with `* -text` so captured bytes survive checkouts. Existing `.gitattributes` files are preserved: review equivalent rules for your selected oracle inputs. `.tada/.gitattributes` preserves captured records independently. Formatting changes still require review; never normalise bytes to carry approval forward.

        For a chore: use the proportionate path above, execute the selected checks, preserve existing approvals and record the result in context. A `tada` business baseline is not mandatory for a documentation edit. Chores need no impact brief document, invented learning or extra approval. For business behaviour changes, return to collaborative discovery and explicit baseline review.
        """;
}
