namespace Primer.Shared.Generation.Greenfield;

/// <summary>
/// The guidance each archetype prescribes. These are templates rather than composed
/// sections because greenfield output states what a project shall be, not what a
/// repository was found to contain: there is nothing to detect, so there is nothing for a
/// section to report having found.
///
/// Every constraint here carries its reason. A rule without one is a rule the next agent
/// overrides on the first inconvenience.
/// </summary>
internal static class ArchetypeTemplates
{
    /// <summary>The template name a web application's guidance is resolved by.</summary>
    internal const string WebName = "agents.web.md";

    /// <summary>The template name a command-line tool's guidance is resolved by.</summary>
    internal const string CliName = "agents.cli.md";

    internal static string NameOf(SolutionArchetype archetype) => archetype switch
    {
        SolutionArchetype.WebApplication => WebName,
        _ => CliName,
    };

    private const string SpeedAndSimplicity = """
        ## Speed Is Not the Goal

        Quality and completeness beat finishing fast. Finish the whole task - edge cases,
        error paths, no stubs or `TODO`s. If it is bigger than it looked, complete it and
        say what it cost rather than quietly narrowing scope.
        """;

    private const string NoArchitectureTests = """
        ### Never write architecture tests

        Never add a test that asserts the shape of the codebase rather than its behavior:
        no structure, layout, or naming tests; no banned-API scans; no traceability tests
        that parse the specifications. Those constraints belong to the compiler, the
        formatter, and review. A test suite exists to prove behavior.
        """;

    internal static string Web { get; } = $$$"""
        # {{ProjectName}}

        ## Purpose

        {{Purpose}}

        {{{SpeedAndSimplicity}}}

        ## Technology

        - Use .NET for the API.
        - Use MediatR, pinned to **12.5.0**. Do not upgrade. 12.5.0 is the last release
          under plain Apache-2.0; from 13.0.0 MediatR is commercially licensed, free only
          under a registered Community tier that lapses above $5M USD annual revenue.
        - Use Microsoft.Extensions libraries and patterns: dependency injection, Options,
          and Configuration.
        - Use Angular for the web client.

        ## Architecture and Design

        - Implement requirements radically simply: the least code that satisfies the
          acceptance criteria, and nothing more. Simple in design, never reduced in scope.
        - Apply SOLID principles throughout the codebase.
        - Organize features and behaviors into vertical slices.
        - Keep back-end code in `backend/`, with source in `src` and tests in `tests`. The
          front end is an Angular workspace at `frontend/`, and every project in it lives
          under `frontend/projects/`.
        - A command-line tool is another project under `backend/src`, not a second root.

        ## Backend

        - Use Clean Architecture. Dependencies point inward. `Domain` references nothing.
        - Keep controllers thin: bind, dispatch through MediatR, return. No logic in a
          controller.
        - Commands, queries, handlers, and validators live in `Application`.
        - One file per type. Every class, interface, record, and enum gets its own file,
          named for the type it holds.
        - Folders and namespaces agree. Controllers live in a `Controllers` folder and are
          namespaced `{{NamespacePrefix}}.Api.Controllers`.

        ## Frontend

        - Organize `frontend/projects/` into `api`, `components`, and `domain` library
          projects, plus one application project that consumes them and launches the app.
        - Prefer signals over RxJS. Reach for RxJS only for genuine streams and events.
        - No single-file components. Template, styles, and class each live in their own file.
        - Keep components presentational. Behavior belongs in services, state in signals.

        ### Interface-driven service consumption - mandatory on the frontend

        Every service an application consumes is reached through an interface and an
        `InjectionToken`. No component, store, or feature imports a concrete implementation.

        - `IQuoteService` declares the behavioral contract and `QUOTE_SERVICE` is its
          `InjectionToken`. The interface, the token, and each implementation live in
          separate files.
        - Contracts are named `I<Entity>Service` in the singular, with no `Api` suffix. The
          `I` prefix marks a swappable contract; data shapes (`QuoteResult`) take no prefix,
          and the production implementation takes the unprefixed name (`QuoteService`),
          never an `Impl` suffix.
        - Consumers call `inject(QUOTE_SERVICE)` only. Application composition binds the
          token to the HTTP adapter in production and to a controlled mock under Playwright,
          so a test never reaches the real implementation.
        - HTTP calls and observable-to-signal conversion stay inside the `api`
          implementations; `domain` types carry no HTTP dependency.

        ## Design System

        The design system is a deliverable in its own right, not a folder inside the front
        end. It sits at `design-system/`, beside `backend/` and `frontend/`, with its own
        `package.json`, its own tests, and its own build, and it deploys as its own static
        site. Build and review a component there before the application consumes it.

        ## Testing Approach

        Use acceptance test-driven development (ATDD):

        - Begin with a failing acceptance test.
        - Link each test to explicit acceptance criteria written using the Given-When-Then
          format.
        - Implement the behavior required to make the test pass.
        - Keep acceptance criteria, tests, and implementation aligned.

        Back end: integration tests against the API.

        Front end: Playwright, using the Page Object Model.

        - One page object per screen. It owns the selectors and the interactions.
        - Tests state intent; page objects know the DOM. Never put a selector in a test.

        {{{NoArchitectureTests}}}

        ## Folder Structure

        ```text
        {{ProjectName}}/
        |-- backend/
        |   |-- src/
        |   `-- tests/
        |-- frontend/
        |   `-- projects/
        |       |-- {{ProjectName}}/
        |       |-- api/
        |       |-- components/
        |       `-- domain/
        |-- design-system/
        |-- e2e/
        |   |-- page-objects/
        |   `-- specs/
        `-- docs/
            `-- specs/
        ```
        """;

    internal static string Cli { get; } = $$$"""
        # {{ProjectName}}

        ## Purpose

        {{Purpose}}

        {{{SpeedAndSimplicity}}}

        ## Technology

        - Use .NET and `System.CommandLine`.
        - Package the application as an installable .NET tool.
        - Use Microsoft.Extensions libraries and patterns: dependency injection, Options,
          and Configuration.

        ## Architecture and Design

        - Implement requirements radically simply: the least code that satisfies the
          acceptance criteria, and nothing more. Simple in design, never reduced in scope.
        - Apply SOLID principles throughout the codebase.
        - Organize features and behaviors into vertical slices.
        - Follow the one-file-per-command pattern: a command's definition and its handler
          live in a single file named for the command.
        - One file per type. Every class, interface, record, and enum gets its own file,
          named for the type it holds.
        - Keep source in `src` and tests in `tests`, both at the repository root.
        - Reach the console and the file system through injected abstractions, never
          through `System.Console` or `System.IO.File` directly, so behavior can be tested.

        ## Testing Approach

        Use acceptance test-driven development (ATDD):

        - Begin with a failing integration test.
        - Link each test to explicit acceptance criteria written using the Given-When-Then
          format.
        - Implement the behavior required to make the test pass.
        - Keep acceptance criteria, tests, and implementation aligned.

        Integration tests against the built command surface are the acceptance tests. Each
        runs against a temporary directory of its own, so the suite is parallel-safe and
        never touches the developer's own working tree.

        {{{NoArchitectureTests}}}

        ## Folder Structure

        ```text
        {{ProjectName}}/
        |-- src/
        |-- tests/
        `-- docs/
            `-- specs/
        ```
        """;
}
