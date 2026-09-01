# Deploying GameHistory to the local test environment

This explains how changes you make in the source project get reflected in the
local IIS testing site.

## How the two folders relate

There are two copies of "GameHistory" on your machine, and they play different
roles:

- **Source project** (this repo, e.g. `C:\Temp\gamehistory`) — the C# source:
  Controllers, Models, Helpers, Views, `Web.config`, etc. This is what you edit
  and what lives on GitHub.
- **Deployed app** (`C:\inetpub\wwwroot\GameHistory`) — the *compiled, running*
  app that IIS serves for your local testing. It is a **build output**, not
  source. It contains `bin\GameHistory.dll` (your compiled C#), the runtime
  `.cshtml` Views, `Content`, a transformed `Web.config`, and the referenced
  DLLs.

You never edit `C:\inetpub\wwwroot\GameHistory` by hand. Instead you **publish**
the source project into it. Publishing compiles your C# into `GameHistory.dll`
and copies exactly the files IIS needs — nothing else.

> Note: before this workflow, the deployed copy was a stale older build
> (it targeted .NET 4.0 and had an older `style.css`). The first publish brings
> it fully in line with the source.

## One-time setup (already done)

A FileSystem publish profile is checked into the repo at:

```
GameHistory\GameHistory\Properties\PublishProfiles\Local_Debug.pubxml
```

It builds in **Debug**, applies the `Web.Debug.config` transform, and deploys to
the standard IIS root `C:\inetpub\wwwroot\GameHistory` by default. Because it
lives in `PublishProfiles`, Visual Studio discovers it automatically.

## Works on any machine

The **source project** location doesn't matter: the script finds the project
relative to itself, and Visual Studio publishes relative to the project. Clone
the repo anywhere and it works.

The **deploy destination** defaults to the standard IIS root
`C:\inetpub\wwwroot\GameHistory`. If your IIS site lives somewhere else, override
it *without editing the committed profile*, in order of precedence:

1. Command line: `.\deploy-local.ps1 -PublishUrl "D:\sites\GameHistory"`
2. Environment variable (honored by both the script **and** the VS Publish
   button): set `GameHistoryDeployPath` to your path, e.g. in PowerShell
   `setx GameHistoryDeployPath "D:\sites\GameHistory"` (reopen VS afterward).
3. Otherwise the profile's default (`C:\inetpub\wwwroot\GameHistory`) is used.

So teammates on the default IIS layout do nothing; anyone relocated sets the env
var once.

## The workflow

Every time you want your changes reflected in the local test site:

### Option A — Visual Studio (one click)

1. Make and save your changes in the source project.
2. In Solution Explorer, right-click the **GameHistory** project > **Publish**.
3. Pick the **Local_Debug** profile and click **Publish**.

Visual Studio compiles the project and copies the output into
`C:\inetpub\wwwroot\GameHistory`. Refresh the site in your browser to see the
changes.

### Option B — Command line (same result, scriptable)

From the repo root:

```powershell
.\deploy-local.ps1
```

The script finds MSBuild (via `vswhere`), builds Debug, and runs the same
`Local_Debug` profile. Use this when you don't want to open Visual Studio, or
to automate the deploy (see below).

## Typical loop

```
edit source  ->  Publish (Option A or B)  ->  refresh browser  ->  repeat
                        |
                        v
        git add / commit / push  (when a change is ready to share)
```

Committing to git and publishing to the local site are independent steps:
git shares your **source** with the remote repo; publishing updates your
**local running site**. Do both — publish to test, commit to save/share.

## Optional: auto-deploy after pulling

If you want the local site refreshed automatically whenever you pull new commits,
add a git `post-merge` hook. Create `.git\hooks\post-merge` (no extension) with:

```sh
#!/bin/sh
powershell -NoProfile -ExecutionPolicy Bypass -File "$(git rev-parse --show-toplevel)/deploy-local.ps1"
```

Then `git pull` will rebuild and redeploy the local site for you.

## Notes

- **Debug vs Release** — this profile uses Debug (full symbols, debugger-friendly),
  which suits a local test environment. To publish Release instead:
  `.\deploy-local.ps1 -Configuration Release`.
- **Web.config** — the deployed `Web.config` is generated from the source
  `Web.config` + the `Web.Debug.config` transform. Don't hand-edit the deployed
  copy; put the change in the source and transforms and republish.
- **Stale files** — the profile keeps existing files in the target
  (`DeleteExistingFiles = False`) so runtime logs aren't wiped. If you ever want
  a clean redeploy, delete `C:\inetpub\wwwroot\GameHistory` first, or flip that
  setting to `True` in `Local_Debug.pubxml`.
- **Prerequisites** — Visual Studio 2017+ or the "Build Tools for Visual Studio"
  must be installed (for MSBuild and the web publish targets).
