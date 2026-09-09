# Issue tracker: GitHub

本專案的 Issues 與 PRD 使用 `scliu0526/last-war-vs-rank-ocr` 的 GitHub Issues，所有操作使用 `gh` CLI 並明確指定 `--repo scliu0526/last-war-vs-rank-ocr`。

## Conventions

- 建立 Issue：`gh issue create --repo scliu0526/last-war-vs-rank-ocr --title "..." --body-file <file>`。
- 讀取 Issue：`gh issue view <number> --repo scliu0526/last-war-vs-rank-ocr --comments`。
- 列出 Issue：`gh issue list --repo scliu0526/last-war-vs-rank-ocr --state open --json number,title,body,labels,comments`。
- 留言：`gh issue comment <number> --repo scliu0526/last-war-vs-rank-ocr --body-file <file>`。
- 修改標籤：`gh issue edit <number> --repo scliu0526/last-war-vs-rank-ocr --add-label "..."`。
- 關閉：`gh issue close <number> --repo scliu0526/last-war-vs-rank-ocr --comment "..."`。

## Pull requests as a triage surface

**PRs as a request surface: no.**

## Publishing

技能要求「publish to the issue tracker」時，建立 GitHub Issue；要求讀取 ticket 時，讀取對應 Issue 的完整內容、留言與標籤。

任務依 dependency order 建立。優先使用 GitHub 原生 issue dependencies 記錄 blocking edges；若 repository 不支援，才在 Issue 的 `Blocked by` 區段列出阻擋 Issue。
