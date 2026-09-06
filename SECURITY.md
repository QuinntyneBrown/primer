# Security Policy

## Reporting a vulnerability

**Do not report security vulnerabilities through public GitHub issues.**

Report them privately through
[GitHub's private vulnerability reporting](https://github.com/QuinntyneBrown/primer/security/advisories/new),
or by email to **quinntynebrown@gmail.com**.

Please include as much of the following as you can:

- The type of issue and the component affected.
- Full paths of the source files involved.
- The configuration required to reproduce it.
- Step-by-step instructions, and a proof of concept if you have one.
- The impact, including how an attacker might exploit it.

You should receive an acknowledgement within **five business days**. If you do not,
please follow up — an unacknowledged report usually means it did not arrive.

## Supported versions

Primer is pre-1.0. Only the latest released version receives security fixes.

| Version | Supported |
|---|---|
| 0.1.x | Yes |

## Scope

Primer's threat surface is unusual for a code generator, because it reads repositories
you may not trust and can change your machine. Reports in these areas are especially
welcome:

- **Escaping the repository boundary.** Any input that causes Primer to read or write
  outside the resolved repository root — traversal segments, absolute paths, symbolic
  links, or junctions.
- **Executing repository content.** Primer starts no child process during `init` or
  `check`. Any input that causes it to execute repository-supplied code is a
  vulnerability.
- **Prompt injection through generated guidance.** Primer treats repository content as
  data and fences it. Any input that causes repository text to be emitted as an
  imperative instruction to an agent, or to escape the fence that labels it as quoted
  data, is in scope.
- **Secret disclosure.** Any input that causes a credential to appear in generated
  output, in logs, or in a diagnostic.
- **Supply chain.** `primer mcp install` resolves pinned versions over HTTPS, verifies
  checksums, and refuses redirects to undeclared hosts. Any bypass of those checks is
  in scope.
- **Configuration tampering.** Any input that causes Primer to corrupt an agent
  client's configuration, or to modify one without consent.

## Out of scope

- Vulnerabilities in dependencies, unless Primer's use of them is what creates the
  exposure. Report those upstream.
- Findings that require an attacker who already has write access to the machine
  running Primer.
- Denial of service caused by pointing Primer at a pathological repository. The
  analysis budget bounds this by design; a report is still welcome if you find a way
  past it.

## Disclosure

Please give a reasonable period to release a fix before disclosing publicly. Credit is
given in the release notes unless you would rather it were not.
