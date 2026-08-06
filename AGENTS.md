# Cat Clip Composer repository instructions

These instructions apply to the entire repository. Re-read this file before changing the project.

## Change workflow

1. Keep the working tree understandable and preserve unrelated user changes.
2. Update the relevant documentation, TODO entries, worklog, and audit log as part of every material change.
3. Build the complete solution after every code change. Fix minor build or test failures and rebuild until clean.
4. Stop and ask the user before proceeding when a failure exposes a large architectural problem, destructive migration, unclear product decision, or meaningful scope expansion.
5. Make a descriptive Git commit for every completed logical change. Use a concise conventional subject and a commit body that explains important behavior, design, verification, and migration details.
6. Do not retain superseded implementations as legacy copies. Git history is the backup unless the user explicitly requests compatibility code.

## Architecture and repository layout

- Keep one Git repository. Do not use Git submodules.
- Organize features as projects, folders, namespaces, and focused classes inside this repository.
- Keep Core independent from UI and infrastructure concerns.
- Keep infrastructure adapters behind Core interfaces.
- Keep WPF presentation logic separate from scanning, persistence, configuration, command construction, and process execution.
- Keep the command-line interface separate from WPF while sharing the same Core and Infrastructure modules.
- Prefer small, single-purpose classes. Split classes that accumulate unrelated responsibilities or complicated branching.
- Extract shared functions and services instead of duplicating logic between GUI, CLI, and tests.
- Avoid speculative abstractions, redundant wrappers, and commented-out or obsolete code.

## Configuration and headless operation

- Persist application configuration in `CatClipComposer.ini` beside the executable (`AppContext.BaseDirectory`).
- Keep generated catalog data and thumbnail caches out of Git.
- Provide headless commands through the CLI project for catalog scanning, listing, rendering, history, configuration inspection, and automation-friendly output/exit codes.
- GUI and CLI workflows must use the same Core contracts and Infrastructure implementations.

## Dependencies and licensing

- The application may use open-source or public-domain dependencies suitable for personal and possible commercial distribution.
- Prefer public-domain, MIT, BSD, Apache-2.0, or similarly permissive dependencies.
- The default application must not require a GPL/AGPL component or force Cat Clip Composer itself to be redistributed under a reciprocal license.
- FFmpeg must remain an external executable. The default render path must work with an LGPL-compatible FFmpeg build and must not require `--enable-gpl` or `--enable-nonfree` components.
- Optional GPL-dependent features may only be added as explicit, clearly documented opt-ins that are not required for normal operation.
- Record every direct and relevant transitive dependency, resolved version, purpose, license, redistribution impact, and source in `docs/STACK_AND_LICENSES.md` and `THIRD_PARTY_NOTICES.md`.
- Run a package vulnerability audit whenever dependencies change and record the result in `docs/AUDIT_LOG.md`.

## Documentation records

- `README.md`: concise user/developer entry point and documentation index.
- `docs/PROJECT.md`: goals, scope, requested features, completed features, and incomplete features.
- `docs/ARCHITECTURE.md`: module boundaries, dependencies, runtime data flow, and design decisions.
- `docs/STACK_AND_LICENSES.md`: software stack, dependency inventory, and desired license policy.
- `docs/CONFIGURATION.md`: INI location, schema, escaping, defaults, and writable-directory behavior.
- `docs/HEADLESS.md`: CLI commands, arguments, output formats, and exit codes.
- `docs/TODO.md`: prioritized work with stable IDs, status, acceptance criteria, and audit TODOs.
- `docs/WORKLOG.md`: chronological append-only summary of material work and associated commits.
- `docs/AUDIT_LOG.md`: append-only architecture, security, licensing, dependency, documentation, and TODO audits.

## Verification

- Required: `dotnet build .\CatClipComposer\CatClipComposer.sln --configuration Release --nologo`.
- Run relevant automated or smoke tests for changed behavior.
- Run `dotnet list .\CatClipComposer\CatClipComposer.sln package --vulnerable --include-transitive` after dependency changes.
- Run `git diff --check` before committing.
- Do not commit `bin`, `obj`, `.vs`, local databases, caches, generated media, or machine-specific files.
