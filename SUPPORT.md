# Support

## How to get help

Primer is a small project maintained in spare time. Support is best-effort, through
GitHub only.

| I want to… | Go here |
|---|---|
| Ask a question or discuss an idea | [Discussions](https://github.com/QuinntyneBrown/primer/discussions) |
| Report something broken | [Open a bug report](https://github.com/QuinntyneBrown/primer/issues/new?template=bug_report.yml) |
| Suggest a change | [Open a feature request](https://github.com/QuinntyneBrown/primer/issues/new?template=feature_request.yml) |
| Report a vulnerability | [SECURITY.md](SECURITY.md) — **not** a public issue |
| Contribute a change | [CONTRIBUTING.md](CONTRIBUTING.md) |

## Before opening an issue

Most reports are resolved faster with three things:

1. The exact command you ran, and its full output.
2. The output of `primer --version`.
3. What you expected instead.

Running with `--verbosity diagnostic` adds the effective configuration and where each
setting came from, which answers most "why did it do that?" questions on its own:

```console
primer init --dry-run --verbosity diagnostic
```

`--dry-run` is always safe: it reports what would change and writes nothing.

## Documentation

- [README.md](README.md) — commands, options, exit codes, configuration.
- [`docs/specs/`](docs/specs/) — what Primer is required to do, in detail.
- [`docs/detailed-designs/`](docs/detailed-designs/) — how each feature works.

## Response expectations

There is no service level agreement. Security reports are prioritised over everything
else; see [SECURITY.md](SECURITY.md) for the acknowledgement window.
