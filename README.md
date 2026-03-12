
# spec-kit ASP.NET Brownfield Demo

This project demonstrates a brownfield AI-assisted development workflow using [spec-kit](https://github.com/github/spec-kit) on an existing .NET CMS. Starting from a cloned open-source repository with ~307,000 lines of C#, Razor, SQL, JavaScript, and config files, two new features were added entirely through structured agents in GitHub Copilot Chat. The steps below document exactly what was done — you can follow the same process to extend your own projects.

> **Note on the workflow:** There was no pre-existing spec-kit constitution or formal specifications in this project. The constitution was generated from scratch by having the agent analyze the existing codebase. The feature specifications are not elaborate formal documents — they are short natural-language prompts describing the desired outcome, as you will see below.

## Acknowledgements

This project is built on top of **[CarrotCakeCMS Core](https://github.com/ninianne98/CarrotCakeCMS-Core)**, an open-source .NET CMS created and maintained by **[Samantha Copeland](https://github.com/ninianne98)**. All credit for the original CMS architecture, feature set, and implementation belongs to Samantha. Please visit the original repository to learn more, contribute, or show your appreciation.

---

## Prerequisites

- [uv](https://docs.astral.sh/uv/) — Python package manager used to install spec-kit
- [VS Code](https://code.visualstudio.com/) with the [GitHub Copilot](https://marketplace.visualstudio.com/items?itemName=GitHub.copilot) extension

---

## Step 1 — Clone the project

```bash
git clone https://github.com/ninianne98/CarrotCakeCMS-Core
cd CarrotCakeCMS-Core
```

This was cloned at commit `77c7d01`.

## Step 2 — Install spec-kit

```bash
uv tool install specify-cli --from git+https://github.com/github/spec-kit.git
```

## Step 3 — Initialize the project

```bash
specify init . --ai copilot
```

This scaffolded the spec-kit structure and installed the Copilot custom agents.

## Step 4 — Open in VS Code

```bash
code .
```

## Step 5 — How to select a spec-kit agent

All spec-kit agents are available in the Copilot Chat **Agent** mode. Open Copilot Chat, click the **Agent** dropdown (top-left of the chat input), and select the agent you want to invoke.

The agents available are:

| Agent | Purpose |
|---|---|
| `speckit.constitution` | Define project-wide principles and governance |
| `speckit.specify` | Generate a feature specification from a description |
| `speckit.clarify` | Ask targeted questions to tighten an existing spec |
| `speckit.plan` | Produce a technical design and implementation plan |
| `speckit.analyze` | Check consistency across spec, plan, and tasks |
| `speckit.tasks` | Generate a dependency-ordered task list |
| `speckit.checklist` | Produce a custom quality checklist |
| `speckit.implement` | Execute tasks from `tasks.md` |
| `speckit.taskstoissues` | Convert tasks into GitHub Issues |

---

## Step 6 — Establish project principles

The **`speckit.constitution`** agent was invoked with:

```
As this is a pre-existing brownfield project I need you to analyze the
codebase exhaustive and in-depth. Do NOT skim over but use multiple
iterations to do a deep analysis and use or create principles focused on
code quality, testing standards, user experience consistency, and
performance requirements. Include governance for how these principles
should guide technical decisions and implementation choices.
```

This created `.specify/memory/constitution.md`, which all subsequent agents respected. Because this is a brownfield project with no prior constitution, the agent derived all principles directly from the existing codebase — no manual pre-authoring was required.

---

## Feature 1 — Docker Compose support

### Step 7 — Write the feature specification

The **`speckit.specify`** agent was invoked with:

```
Add Docker Compose support so developers can run and test the CMS on
Windows, macOS, and Linux without installing or configuring SQL Server
locally. Docker Compose is responsible only for infrastructure: spin up a
SQL Server container, initialize both the CarrotCoreMVC and Northwind
databases using scripts from the CMSDataScripts SQL project, and expose
SQL Server on localhost so the host-running dotnet run (CMSAdmin) and
dotnet test processes can connect to it. Replace the existing Windows
Integrated Authentication connection strings (Trusted_Connection=True
against .\SQL2016EXPRESS) with SA password authentication pointing to
localhost, supplied via environment variables or a local .env file so
existing appsettings.json files are not modified. Include a persistent
volume for database data, a health check so the app only starts after SQL
Server is ready, and clear developer instructions for the docker compose
up then dotnet run workflow. All credentials must be environment-variable
driven with no secrets in source-controlled files. Containerizing CMSAdmin
itself is out of scope.
```

### Step 8 — Create the implementation plan

The **`speckit.plan`** agent was invoked with:

```
Execute and keep in mind that when dealing with Docker Compose it must
use the new style `docker compose`.
```

### Step 9 — Generate the task list

The **`speckit.tasks`** agent was invoked with:

```
Execute
```

### Step 10 — Implement

The **`speckit.implement`** agent was invoked with:

```
Execute
```

Three passes were needed to complete this feature:

**Pass 1** — `speckit.implement` was run with `Execute`. The agent completed tasks T001–T014 and flagged T015 as a manual step.

**Pass 2** — `speckit.implement` was run again with `Execute`. The agent once more surfaced T015 as requiring manual action.

**Pass 3** — `speckit.implement` was prompted with `Perform the manual steps`. The agent completed T015 and all remaining tasks.

---

## Feature 2 — Headless REST API

### Step 11 — Write the feature specification

The **`speckit.specify`** agent was invoked with a brief, informal prompt — no upfront design work, no formal requirements document:

```
Add a public read-only REST API to the CMS that exposes pages, blog posts,
navigation trees, content snippets, and widget zones as JSON endpoints.
API consumers should be able to query content by URL slug, category, tag,
date range, and site ID for multi-site setups. Secure the API with
token-based authentication so it can serve as a headless backend for SPAs,
static site generators, or mobile apps.
```

This is a good example of how lightweight the input can be: a few sentences of intent were enough for `speckit.specify` to produce a full structured specification.

### Step 12 — Create the implementation plan

The **`speckit.plan`** agent was invoked with:

```
Execute and keep in mind that we also need simple scripts to test the API
ourselves
```

### Step 13 — Generate the task list

The **`speckit.tasks`** agent was invoked with:

```
Execute
```

### Step 14 — Implement

The **`speckit.implement`** agent was invoked with:

```
Execute
```

---

## Result

A production-quality brownfield CMS extended with two fully implemented features — cross-platform Docker Compose infrastructure and a token-authenticated headless REST API — built through structured agent workflows without manual scaffolding.
