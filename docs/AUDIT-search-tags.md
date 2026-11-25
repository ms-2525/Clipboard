# AUDIT — `ClipTags`, `ClipSearch`, and the `search` bridge

**Date:** 25 Nov 2026 · **Mode:** adversarial, read-only · **Commits:** `1b57ecd`, `ffc97aa`
**Worktree:** `C:\Users\mayan\.local\share\opencode\worktree\e34e10\v2-stack`
**Contract read first:** `docs\DESIGN-language.md` §3.1 (68–87), `docs\STEP-PLAN-export-tags-ocr.md` (24–40, 53–55)
**Harness:** `%TEMP%\opencode\audit-search-tags` — links the **real** `Core\ClipSearch.cs`, `Core\ClipTags.cs`,
`Core\ClipboardItem.cs`, `Core\SearchService.cs`. Command: `dotnet run -c Release` (output saved to
`result.txt`). Real-data pass uses a **copy** of `%TEMP%\opencode\phase1-db\clip.db`, opened read-only.
No app code changed; no live database touched.

**Snapshot note:** the worktree advanced to `93ab0a1` while this audit ran. `Core\ClipTags.cs` is
unchanged since `1b57ecd`; `Core\ClipSearch.cs` differs from `ffc97aa` only in `Contains` delegating to
the new shared `SearchService.Contains` (same OrdinalIgnoreCase behaviour), so the classifier and
grammar results are identical at the audited commits. Host later renamed the DTO's truncation fields
(`MoreCount` → `Truncated`/`RemainingChars`); both `getItems` and `search` share that DTO at either
commit. Uncommitted S3b UI work appeared during the audit and is called out separately at the end — it
is **not** part of the two audited commits.

**The product rule under test:** a tag appears only on a positive test. A missing tag is allowed; a
wrong tag is a product failure because search becomes untrustworthy.

---

## Verdict by area

| # | Area | Verdict | One line |
|---|---|---|---|
| 1 | Classifier false positives | **FAIL** | 19/21 prompt cases clean, but **5 regex mechanisms tag ordinary prose**: TypeScript (6 repros), Python `import`/`from`, JavaScript `function (`, Python `class X (`, SQL DDL. |
| 2 | Classifier false negatives | **RISK** | Every miss is allowed by §3.1, but `excel:`/`csv:` will miss pasted CSV/TSV/browser tables — the rule proves less than the step-plan story implies. |
| 3 | Grammar (`Parse`) | **PASS with quirks** | 30/30 inputs safe and predictable; `- image:` returns nothing and a quoted `"-foo"` cannot be searched positively. |
| 4 | `Matches` correctness | **PASS** | 19/19; 0 bytes allocated over 10,000 calls; exclude always beats include; 1 MB body, 5,000 tags, Devanagari all fine. |
| 5 | Consistency / drift | **FAIL (drift)** | Aliases, `code:` union and the Core/Host alias table pass; `SearchService.Filter` and `ClipSearch` disagree on 2 of 3 probes; an **uncommitted UI copy** breaks one-home (out-of-scope note). |
| 6 | Performance | **RISK** | 1k rows ≈ 25–30 ms (pass); 10k rows ≈ 330–390 ms (fail); no per-call regex, no LINQ in the hot path. |
| 7 | Bridge integration | **PASS with risks** | MaxItems/tabs, paging, `unknownTags`, DTO shape all correct by inspection; `Meta()` has a 32,767-parameter cliff that silently erases formats; the method is not reachable from the current UI. |

---

## 1. Classifier false positives

The prompt's cases, observed tags (harness, real `ClipTags.Classify`, formats passed as a list row
would carry them):

| Case | Observed | Verdict |
|---|---|---|
| CA email mentioning “select the ledger from the list” | `[]` | PASS |
| Windows path inside a paragraph | `[]` | PASS |
| URL inside a sentence | `[]` | PASS |
| single decimal in a sentence | `[]` | PASS |
| “color #FF6B6B is the brand accent” | `[]` | PASS |
| JSON-looking sentence with a colon | `[]` | PASS |
| `C:\Program Files\App` | `[]` | PASS (space → intentional miss) |
| `\\server\share\folder` | `path` | **contract-correct** — §3.1 says the whole text is one UNC path. The prompt expectation contradicts the frozen rule. |
| “Delete from the register” (with the article) | `[]` | PASS |
| 3-word probes `function call failed`, `const value found` | `[]` | PASS |
| RTF-only clip (Text null) | `[]` | PASS |
| HTML fragment mid-sentence (`This is <b>bold</b> …`) | `[]` | PASS |
| emoji-only clip | `[]` | PASS |
| single character | `[]` | PASS |
| whitespace only | `[]` | PASS |
| `NaN` | `[]` | PASS |
| `1,23,456.78` | `[]` | PASS (Invariant `Float` has no thousands separator) |
| phone `+91 98765 43210` | `[]` | PASS |
| PAN `ABCDE1234F` | `[]` | PASS |
| GSTIN `27ABCDE1234F1Z5` | `[]` | PASS |
| account number `123456789012` | `number` | **contract-correct** — §3.1 says “entire text parses as one number”. If the owner wants account numbers untagged, that is a **new rule**, not a bug. |

**19/21 clean; the two exceptions are the prompt fighting the contract, not the code.** That is the
good news. The bad news is outside the fixture table:

### F1 — P0 — TypeScript type-annotation regex tags ordinary CA prose

`Core\ClipTags.cs:53-55`:

```csharp
@"\b(?:interface|type)\s+[A-Z]\w*\s*[={]|:\s*(?:string|number|boolean|unknown|any|void)\b"
```

The second alternative matches **any colon followed by a type word**, which is ordinary English.

| Repro input | Observed | Expected |
|---|---|---|
| `Note: any changes will be lost.` | `typescript` | `[]` |
| `Cheque status: void` | `typescript` | `[]` |
| `Payment mode: any` | `typescript` | `[]` |
| `Status: unknown` | `typescript` | `[]` |
| `The columns are: number, name and amount.` | `typescript` | `[]` |
| `return type: void` | `typescript` | `[]` |

This is the worst failure in the audit: a cheque-status or payment-mode note — exactly what a CA
copies — gets a **TypeScript** tag, and `code:` search then returns it. It violates the one rule the
classifier exists to keep. The fixture table (27/27) does not contain a prose sentence with `: any`,
`: void`, `: number`, etc.

**Smallest correct fix:** keep the structural alternative; require a code context for the annotation:

```csharp
static readonly Regex TsAnnotationRe = new Regex(@":\s*(?:string|number|boolean|unknown|any|void)\b", ...);
static readonly Regex CodeContextRe  = new Regex(@"\b(?:function|const|let|var|class|import|export)\b|=>", ...);

if (TsStructuralRe.IsMatch(text) ||
    (TsAnnotationRe.IsMatch(text) && CodeContextRe.IsMatch(text))) return "typescript";
```

Real `interface User { name: string }` still hits the structural rule; `let x: string = ""`,
`function f(): void {}`, `const f = (a: string) => a` still pass via context; a bare `x: string`
fragment becomes a miss (allowed). Add the six prose rows above to the fixture gate.

**Blocks release: YES.** This alone invalidates the “0 false positives” claim for S1.

### F2 — P0 — Python `import` / `from` tags prose

`Core\ClipTags.cs:61-63` matches `^\s*import\s+[a-z_][\w.]*` and
`^\s*from\s+[a-z_][\w.]*\s+import\s+` with no end anchor.

| Repro input | Observed | Expected |
|---|---|---|
| `import data from the bank portal` | `python` | `[]` |
| `from bank import statement` | `python` | `[]` |

The first is a normal finance instruction, not valid Python (`import data from …` cannot parse).
The second is valid Python *and* English; accept that ambiguity, but the first must not tag.

**Smallest correct fix:** make both forms module-shaped and line-anchored:

```csharp
^\s*import\s+[a-z_][\w.]*(?:\s*,\s*[a-z_][\w.]*)*\s*(?:#.*)?$
^\s*from\s+[a-z_][\w.]*\s+import\s+[A-Za-z_*][\w.*]*(?:\s*,\s*[A-Za-z_*][\w.*]*)*\s*(?:#.*)?$
```

`import os`, `import os, sys`, `from pathlib import Path` still tag. `import data from the bank portal`
no longer does. **Blocks release: YES** (ordinary prose, wrong tag).

### F3 — P1 — JavaScript `function (` tags prose

`Core\ClipTags.cs:57-59` has `\bfunction\s*(?:[A-Za-z_$][\w$]*\s*)?\(` with no body requirement.

| Repro | Observed | Expected |
|---|---|---|
| `This function (callback) runs after the write.` | `javascript` | `[]` |

**Fix:** require the body brace: `…\)\s*\{`. Real `function foo() {}` / `function () {}` still tag.
**Blocks release: YES** — same product rule, lower frequency.

### F9 — P2 — Python `class X (` tags prose

| Repro | Observed | Expected |
|---|---|---|
| `class Reunion (2024) tickets are on sale.` | `python` | `[]` |

`class\s+[A-Z]\w*\s*[:(]` accepts the `(` of an English parenthetical. **Fix:** require the colon —
`class\s+[A-Z]\w*\s*(?:\([^)\r\n]*\))?\s*:` — in the same Python edit. **Blocks release: NO** (uncommon
shape; fix in the same batch).

### F10 — P3 — SQL DDL prose

| Repro | Observed | Expected |
|---|---|---|
| `Drop table reservations, please.` | `sql` | `[]` |
| `Delete from register` | `sql` | `[]` (defensible: it is exactly valid SQL) |

`SqlRe` (`ClipTags.cs:39-41`) matches the DDL prefix and ignores the trailing comma/word. **Fix:** require
a terminator for DDL (`DROP/CREATE/ALTER … ;`), accepting a one-liner without `;` as a miss. **Blocks
release: NO.** `Delete from the register` (with “the”) correctly returns `[]`; the no-article form is a
genuine ambiguity, not a clear bug.

### F11 — P3 — whole-text shapes that are also valid prose tokens

| Repro | Observed | Why it is not a P0 |
|---|---|---|
| `logo@2x.png` | `email` | Syntactically a valid address; this is a filename in practice. Fix would be “first domain label starts with a letter”, which still misses other cases. |
| `#facade` | `color` | A hashtag that is also exactly a 6-digit hex colour. The frozen rule cannot distinguish. |
| `+91` | `number` | `double.TryParse("+91")` is 91. The rule is exact. |

These are **rule limits, not implementation defects**. Record them as accepted ambiguity or narrow the
rules deliberately; do not “fix” one without writing the new rule down.

**Observed on real data:** the 38-row phase1 DB copy produced 6 tags — `[files,path]`, `[image]`,
`[link]`, `[path]`, `[link]`, `[spreadsheet]` — all correct, **0 false positives**. The real corpus is
too small to contain the prose shapes above; that is why the fixture gate must carry them explicitly.

---

## 2. Classifier false negatives

| Clip | Observed | Under “missing is allowed” | Product consequence |
|---|---|---|---|
| CSV pasted as text `a,b,c\n1,2,3` | `[]` | Acceptable | `csv:`/`excel:` will not find it |
| Markdown table | `[]` | Acceptable (no markdown tag exists) | — |
| TSV block, no Biff | `[]` | Acceptable by §3.1 | Browser/Google-Sheets tables copied as text are never `spreadsheet` |
| TSV with `Biff12` | `spreadsheet` | Correct | Real Excel copies carry Biff (real row 370 proves it) |
| Quoted path `"C:\…\notes.txt"` | `[]` | Acceptable (the whole text is not a path) | Users who copy a path with quotes lose the tag |
| URL with trailing newline | `link` | Correct (`.Trim()` at `ClipTags.cs:117`) | — |
| Multi-line shell, no shebang | `[]` | Acceptable (shell rule is shebang-only) | Common copy-paste scripts untagged |
| SQL with leading `--` comment | `[]` | Acceptable (“begins with a SQL verb”) | Very common real query is untagged |
| HTML with leading newline **and comment** | `[]` | Acceptable (“starts with `<` + known tag”) | Saved pages starting with a comment are untagged |
| HTML with leading newline only | `html` | Correct | — |
| Python shebang `#!/usr/bin/env python3` | `[]` | Acceptable, but the contract lists “shebang” generically | The most common shebang is not a shell shebang |
| `1,000` | `[]` | Acceptable (Invariant Float) | The CA's most common number format is untagged |
| Browser table (`HTML Format` + TSV) | `[]` | Acceptable by the frozen rule | `excel:` will miss tables copied from a browser |
| Excel sheet whose format blobs exceeded the 10 MB budget | `[]` | Acceptable | Huge sheets lose the tag silently |

**Verdict: RISK, not FAIL.** No miss is a wrong tag. But two are product-level mismatches worth a
decision before the owner is told “type `excel:` and see the spreadsheet clips”:

1. **`csv:` is an alias for a rule that does not prove CSV.** Excel itself puts a `Csv` format on the
   clipboard (real row 370 has it), but `ScanFormat` ignores `Csv`; a CSV *file* drop is `files`.
   Typing `csv:` finds only Biff copies. Either add `Csv` to `ScanFormat` (it is proof the payload is
   CSV text) or state in the guide that `excel:` means “copied from Excel”, not “is a spreadsheet”.
2. **Browser tables are not spreadsheets by this rule.** The step plan's promise (“excel: shows the
   spreadsheet clips”) will under-deliver for Google Sheets/Chrome. That is the price of proven-only;
   it needs an owner sentence, not a silent gap.

---

## 3. Grammar — `Parse`

Harness printed terms / include / exclude / unknown for each input. Verdict is mine.

| Input | Parse result | Sensible? |
|---|---|---|
| `image:` | include `[image]` | Yes |
| `IMAGE:` | include `[image]` | Yes (lower-cased) |
| `image :` | terms `[image, :]` | Predictable (“a tag word needs the colon”) but a user who adds a space gets a text search. Note. |
| ` image :` | terms `[image, :]` | Same |
| `-image:` | exclude `[image]` | Yes |
| `--image:` | term `[--image:]`, negated match `-image:` | Yes enough (not a tag) |
| `"image:"` | term `[image:]` | Yes (quoted = literal) |
| `foo:bar` | term `[foo:bar]` | Yes (not tag-shaped) |
| `a:b:c` | term `[a:b:c]` | Yes |
| `:` | term `[:]` | Yes |
| `::` | term `[::]` | Yes |
| `C:` | term `[C:]` | Yes (one-letter drive refused, `ClipSearch.cs:118`) |
| `C:\` | term `[C:\]` | Yes |
| `https://` | term `[https://]` | Yes |
| `mailto:a@b.c` | term | Yes |
| `tel:+91` | term | Yes |
| `"unterminated` | term `[unterminated]` | Forgiving; sensible |
| `""` | no terms → matches all | Sensible, though “quotes only” shows everything |
| `"" ""` | no terms → matches all | Same |
| 10,000-char query | 1 term of 10,000 chars | Yes; no crash, no allocation blow-up (0.096 ms/query) |
| 200 tokens | 200 terms | Yes; no cap on term count (see perf note) |
| tabs/newlines as separators | 3 terms | Yes (`char.IsWhiteSpace`) |
| `-"foo bar"` | term `[-foo bar]` (excluded) | Yes |
| `- image:` | terms `[-]`, include `[image]` | **Quirk: returns nothing** — the lone `-` becomes a required literal. A user meaning “not images” gets an empty list. |
| `छवि:` | term `[छवि:]` | Yes (non-Latin cannot be a tag; searches literally) |
| `图片:` | term `[图片:]` | Yes |
| `123:` | term `[123:]` | Yes (tag names start with a letter) |
| `image: -image:` | include + exclude `[image]` | Correct: exclude wins (verified) |
| `-` | term `[-]` | Yes, literal |
| `"-foo"` | term `[-foo]` **then interpreted as negated** | **Quirk: a quoted literal `-foo` cannot be searched positively.** `Matches` derives the sign from `term[0]`, so quoting does not escape it. |

Other parser notes:

- `http:` is tag-shaped but unknown → warning `[http]` + text search for `http:`. Fine, but a user
  pasting a URL scheme fragment gets a “unknown tag” hint.
- `image:,` (trailing comma) is text, not a tag. Fine by “the colon is the whole token”.
- A lone `-` (length 1) is a positive literal, not negation — consistent with `Matches` (`term.Length > 1`).
- No OR, no parentheses, no escaping. Exactly as the step plan says.

**Verdict: PASS with two P3 quirks worth fixing cheaply** (lone `-` before a token; quoted leading `-`).
The 10k/200-token inputs are safe.

---

## 4. `Matches` correctness

All 19 checks passed; no exceptions.

| Check | Result |
|---|---|
| null `Text` + `WindowTitle` | found (`Contains` falls back at `ClipSearch.cs:212-218`) |
| null `Text` + `Uris` | found |
| null `Text`, absent term | not found |
| empty **string** `Text` + `WindowTitle` | **not found** — the fallback is `if (item.Text == null)`, so `""` blocks title/URI search (**P3**, see F14) |
| null tags list + include | not found (correct) |
| null tags list + exclude | found (correct: no tags means the tag is absent) |
| empty item + empty query | true |
| null item + empty query | true |
| null query | true |
| duplicate/null tags, include | true |
| duplicate/null tags, exclude | false |
| excluded text term, different case | false (OrdinalIgnoreCase) |
| `image: -image:` on an `image` item | false — **exclude beats include** |
| phrase with internal double spaces | exact match true; single-space text false (exact phrase, as typed) |
| Devanagari substring / exclusion / tag-shaped fallback | true / true / true, no throw |
| non-Latin unknown tag recorded | none (correct: cannot be a tag name) |
| 1 MB body, term at the end | true, **0.07 ms** |
| 5,000 tags, last tag include + `code:` | true / true, **0.94 ms** |

**Verdict: PASS.** `Contains` uses spans (`ClipSearch.cs:203-220`), `HasTag` is a linear scan
(`222-234`), no LINQ, no allocation. The only wart is `== null` instead of `IsNullOrEmpty`.

---

## 5. Consistency and drift

### What is solid

- `excel:`, `spreadsheet:`, `sheet:`, `xls:`, `csv:` all canonicalise to `spreadsheet`
  (`ClipSearch.cs:149-155`) and returned the **identical id set** `[10]` on the fixture.
- `code:` returned **exactly** ids 1–9, the nine language fixtures, and no other item; the emitted
  language set was exactly `{csharp, javascript, typescript, python, rust, php, go, java, shell}`.
  `LanguageTags` (`ClipSearch.cs:14-17`) has the same nine as `Language()` (`ClipTags.cs:190-202`).
- The alias table has **one home**: `Core\ClipSearch.cs:130-191`. Grep found no second copy under
  `Host\`; at the audited commits the UI had none either. The uncommitted `tags.ts` duplicates the
  **classifier** (not the aliases) — see the out-of-scope note below.
- Tag ids are lower-case everywhere; `code:` is tested in `HasTag` (`225-230`), not by a fake tag.

### F6 — P2 — the two text-search rules disagree

`Page(query)` still uses `SearchService.Filter` (`Host\ClipHost.Queries.cs:25-27`).
`Search(query)` uses `ClipSearch.Matches` (`Host\ClipHost.Queries.cs:59-77`).

| Query | `SearchService.Filter` | `ClipSearch` | Why |
|---|---|---|---|
| `alpha beta` over texts `"alpha beta"` and `"beta alpha"` | `[101]` | `[101,102]` | Filter is one whole-query substring (`SearchService.cs:15-16`); ClipSearch is AND terms (`ClipSearch.cs:87-93`) |
| `budget` over an image row with `Text=null`, `WindowTitle="Budget.xlsx"` | `[]` | `[103]` | ClipSearch searches `Uris`/`WindowTitle` when Text is null (`212-218`); Filter never does |
| `alpha` | `[101,102]` | `[101,102]` | same substring for one word |

There is a **second** difference: `Page` loads `GetRecent(-1)` for a query (unbounded, line 25), while
`Search` loads `GetRecent(MaxItems)` for the main history (`line 61-63`). For historical rows beyond a
lowered cap, `search` finds fewer clips than `getItems` did.

The comment at `ClipSearch.cs:80-81` says the *comparison* is the same as `Filter` — true — but a reader
will infer the *result set* is the same. It is not. Today the UI does not call `search` (see §7), so no
user sees this yet. The moment S3b wires the endpoint, plain-word search changes meaning.

**Smallest correct fix:** one rule for the bridge. Delete the `query` branch from `Page`/`getItems`
(or make it call the same `ClipSearch` path) and make `search` the only text-query endpoint; use the
same candidate set (`GetRecent(MaxItems)`) on both. The old WPF UI uses `SearchFull`, not this bridge,
so the blast radius is small. **Blocks release: NO, blocks S3b sign-off: YES.**

Note: this is separate from the **already-recorded** P1 “two search rules disagree” about case folding
(`AUDIT-db-logic.md:22`, `lower()` vs OrdinalIgnoreCase). At the audited commit that P1 is open; HEAD's
`5a7a285` fixes the comparison by sharing `SearchService.Contains`, but **not** the term semantics
above. Both must be decided, not merged.

---

## 6. Performance

Measured on this machine, Release, no debugger. Text bodies are 600 chars unless stated (the store
preview is 1,200 chars, so this is realistic-to-pessimistic for row size).

| Measurement | Result |
|---|---|
| `Parse` 2,000 distinct queries (term + include + exclude + quoted phrase + unknown tag) | **1.9 ms total**, 0.096 ms/query, 1,702 KB allocated |
| `Matches` 2,000 items × 5 passes, 3 tags, terms + include + exclude | **1.7 ms/pass**, **0 KB over 10,000 calls** |
| `Classify` 2,000 × 600-char prose, no tag | **24.6 ms/pass** (12.3 µs/row), 0 KB (no tag → `Array.Empty`) |
| `Classify` 2,000 tagged rows | 2.4 ms total, **234 KB** = 117 B/row (List + array) |
| `Classify` one 1,155-char prose row ×100 | 1.8 ms → **18 µs/row** |
| `Classify` one 1 MB prose | **3.3 ms** (9 regex scans) |
| `Classify` 10,000 × 600 chars (`MaxItems` upper bound) | **124–180 ms** |
| `Meta()` 200 ids / 1,000 / 10,000 | 3.0 / 3.2 / **206 ms** |
| `Meta()` 32,766 ids | 2.4 s |
| `Meta()` 32,767 ids | **throws `too many SQL variables`** |
| `Meta()` 10,000 ids in 500-id chunks | **47.5 ms total**, same rows, no cliff |

Conclusions:

- **No regex is compiled per call.** Every pattern is `static readonly Regex` with
  `RegexOptions.Compiled` at `ClipTags.cs:19-83`; `ClipSearch.cs` contains no regex at all.
- **No LINQ in the hot path.** `Matches` and `Contains` are index loops (`ClipSearch.cs:87-100`,
  `203-241`); neither file has `using System.Linq`. `Host.Search` uses LINQ only to build the id list
  and the page (`Host\ClipHost.Queries.cs:66,84`), once per request.
- **1k rows: PASS** on the 50 ms budget (~25–30 ms: classify 12–18 ms + meta 3 ms + DTO/load). The
  tracker's “3–30 ms at 1k/2k” is plausible at 1k, optimistic at 2k where classification alone is
  24.6 ms for 600-char bodies.
- **10k rows: FAIL** the budget. `Search` runs on the UI thread (`ClipHost.cs:44-50`), so
  `Classify` (124–180 ms) + `Meta` (206 ms) ≈ **330–390 ms** per tag keystroke, before the
  `GetRecent` load. The tracker already records a P1 “bridge search blocks the UI thread” at
  531 ms / 9.5k (`BUILD-TRACKER.md:53`); this slice does not fix it and adds classification.
- **`Classify` allocation claim is wrong.** `ClipTags.cs:12-13` says “the only allocation is the
  returned array”. `Add` also allocates a `List<string>` (`204-208`). Measured 117 B per tagged row.
  Minor doc fix (P3).
- **No cap on query terms.** 200 tokens are accepted; on 1k × 1.2k-char rows that is up to 240 M
  character comparisons if the first terms are common. A 32-term or 512-char cap would bound it
  (P3, not measured as a freeze).

---

## 7. Bridge integration — `Host\ClipHost.Queries.cs::Search`

| Question | Answer | Evidence |
|---|---|---|
| Main history uses `Config.MaxItems`? | **Yes** — `GetRecent(Math.Max(1, _cfg.MaxItems))` | `:61-63` |
| Tab searches all items? | **Yes** — `GetTabItems(-1, tabId)` (the store comment says `max -1 = no limit`) | `:61-62` |
| Paging beyond the end correct? | **Yes** — `for (i = offset; i < hits.Count && items.Count < limit; i++)`, `Total = hits.Count` | `:86-93` |
| Classifies every row every call? | **Yes, when any tag is present** (`byTag`); for a word query only the returned page | `:65-79`, `:83-92` |
| `unknownTags` survives to the reply? | **Yes** — public `UnknownTags`; the host serializer is camelCase (`ClipHost.cs:19-22`) so the wire name is `unknownTags` | `:93`, `:270-275` |
| Same DTO shape as `getItems`? | **Yes** — the same nested `ItemDto`; `SearchResult` is `PageResult` + `unknownTags` | `:264-294` |
| Reachable from the current UI? | **No** — `Host` has no `search` method and no call site; `ClipItem` has no `tags` field; `getItems` still sends `query` to `Page`. The in-progress S3b UI filters `tag:` locally with its own classifier (out-of-scope note at the end). | `UI\web\src\host.ts:14-29, 82-91, 200-217` |

The last row matters for acceptance, not for these two files: “type `image:` and see only pictures”
cannot be checked until S3b adds the method to the typed host and the search box calls it. The tags are
already in the `getItems` reply, but `normalizeItem` spreads them through untyped and nothing consumes
them (`host.ts:124-140`).

### F5 — P2 — `Meta()` silently loses formats (and therefore tags) at 32,767 ids

`Search` calls `Meta(all.Select(...))` before filtering (`:66`). `Meta` builds `$p0 … $pN` and one
`IN (...)` list (`:166-178`). SQLite's parameter maximum is 32,766 in this build:

```
Meta ids=32766: ok, rows=10000, 2425 ms
Meta ids=32767: THROWS SqliteException: 'too many SQL variables'
```

`Meta` catches everything and returns the partial map (`:192-195`), with the comment “metadata is
cosmetic (badges)”. **That comment is now false:** formats are the only proof of `files` and
`spreadsheet` (`ClipTags.cs:142-153`). On a large tab, every row gets empty formats, so `excel:` and
`files:` return nothing and the reply looks valid. The user sees an empty result, not an error.

Also note the cost even below the cliff: **206 ms** for 10,000 ids, on the UI thread. The lazy fix is
chunking, measured at **47.5 ms for 10,000** in 500-id chunks — faster *and* below every limit:

```csharp
for (int start = 0; start < ids.Count; start += 500) { /* same SQL, ids.GetRange(start, ...) */ }
```

and change the catch comment to say tags depend on it. **Blocks release: NO** at default 200; **YES
before a pinned/custom tab is allowed to grow past ~32k**, and the 10k cost belongs with the existing
P1 thread work.

### F17 — P3 — `Search` on an empty query reports a different total than `getItems`

`Search("")` loads `GetRecent(MaxItems)`, `Total = hits.Count` (≤ MaxItems), while `Page("")` reports
the full `counts` total. If the UI ever calls `search` for an empty box, the “N clips” count changes
from the tab count to 200. Guard the UI to call `search` only for a non-empty query, or give `Search`
the same total semantics. No current impact.

---

## Findings, ranked

Severity scale: **P0** wrong tag on ordinary prose / release-blocking under the §3.1 rule · **P1**
wrong tag on plausible prose, or a silent correctness loss · **P2** wrong tag on uncommon shapes,
drift that will bite a later slice, or a measured budget failure outside the default · **P3** polish,
docs, or allowed miss.

| ID | Sev | Finding | Repro → observed | Expected | Smallest correct fix | Blocks release? |
|---|---|---|---|---|---|---|
| F1 | **P0** | TypeScript annotation regex tags prose | `Cheque status: void` → `typescript`; `Note: any changes…`, `Payment mode: any`, `Status: unknown`, `columns are: number`, `return type: void` → same | `[]` | Require a code-context token with the annotation; keep the structural `interface/type` rule | **YES** |
| F2 | **P0** | Python `import`/`from` tags prose | `import data from the bank portal` → `python`; `from bank import statement` → `python` | `[]` | Line-anchor and module-list-shape both forms | **YES** |
| F3 | **P1** | JavaScript `function (` tags prose | `This function (callback) runs after the write.` → `javascript` | `[]` | Require `{` after the parameter list | **YES** |
| F4 | P1 | Tag search blocks the UI thread at 10k | 10k: classify 124–180 ms + Meta 206 ms + load | < 50 ms end-to-end | Background connection / cache; the tracker's S3e | **YES if MaxItems > ~2k; no at default 200** (already tracked) |
| F5 | P2 | `Meta()` 32,767-id cliff silently erases formats | 32,767 ids → `too many SQL variables`, catch returns `{}` → `excel:` empty | Same tags as a 1k tab | Chunk at 500 ids (47.5 ms/10k); fix the “cosmetic” comment | NO (until a tab can exceed 32k) |
| F6 | P2 | `Filter` vs `ClipSearch` disagree | `alpha beta` → `[101]` vs `[101,102]`; image title `budget` → `[]` vs `[103]` | One rule | Route all bridge queries through `ClipSearch`; one candidate set | NO; blocks S3b |
| F7 | P2 | Preview truncation can mint a wrong tag | full `C:\x.txt` + 1,300 spaces + `notes` → `[]`; its 1,200-char preview → `path`; same for `number` and `json` | `[]` (full text is not a path/number/JSON) | `bool whole = !item.IsTruncated;` (HEAD; at the audited commit `item.TextLength <= ClipboardStore.PreviewChars`) and gate link/email/path/colour/number/JSON | NO (rare shape), fix in the same classifier batch |
| F8 | P2 | `excel:`/`csv:` under-deliver for CSV/TSV/browser tables | `a,b,c\n1,2,3` → `[]`; browser TSV → `[]` | Owner decision | Add `Csv` to `ScanFormat` (proof the payload is CSV) or state the limit in `USER_GUIDE` | NO; owner decision before the step-1 check |
| F9 | P2 | Python `class X (` tags prose | `class Reunion (2024) tickets…` → `python` | `[]` | Require the class colon | NO |
| F10 | P3 | SQL DDL prose | `Drop table reservations, please.` → `sql` | `[]` | Require `;` for DDL | NO |
| F11 | P3 | Ambiguous whole-text shapes | `logo@2x.png` → `email`; `#facade` → `color`; `+91` → `number` | `[]` by user intent | Write a narrower rule or accept | NO |
| F12 | P3 | Parser quirks | `- image:` → `[]` for a non-image item; `"-foo"` → excludes `foo`; `image :` → text; `""` → all | Intuitive negation | Consume a lone `-` as the next token's sign; store negation separately from the term text | NO |
| F13 | P3 | Allowed false negatives | quoted path, shell without shebang, SQL comment, HTML comment, Python shebang, `1,000`, `<br/>`, `WITH … SELECT` → `[]` | Tagged | None required; record the misses | NO |
| F14 | P3 | Empty **string** `Text` blocks title/URI search | item `Text=""`, `WindowTitle="Budget 2026"`, query `budget` → false | true | `if (string.IsNullOrEmpty(item.Text))` (`ClipSearch.cs:212`) | NO |
| F15 | P3 | Tag search not wired to the bridge | `search` absent from `Host`, `ClipItem` has no `tags`; `getItems` still uses `Page`; S3b derives tags locally instead | Type `image:` and see pictures | Add `search` + `tags` + `unknownTags` in S3b; delete the local copy | NO (expected slice boundary) |
| F16 | P3 | Allocation comment wrong | 2,000 tagged rows → 234 KB; source says “only … returned array” | Comment matches code | Mention the `List` or use a stack/array builder | NO |
| F17 | P3 | `Search("")` reports a different total | empty query → `Total = hits.Count` (≤ MaxItems) vs `Page`'s full count | Same “N clips” count | Guard the UI to call `search` only for non-empty queries, or align the total | NO |

### Out-of-scope observation — the uncommitted S3b UI duplicates the classifier (must not ship)

`UI\web\src\tags.ts` is **untracked** at the time of this audit and is not part of `1b57ecd`/`ffc97aa`.
It re-implements §3.1 in TypeScript and already disagrees with `Core\ClipTags.cs`:

- `PATH_RE = /^(?:[a-z]:\\|\\\\)\S[^\n]*$/i` allows spaces after the first character:
  `C:\Program Files\App` (and a path followed by prose on the same line) gets **Path** in the UI; the
  server leaves it untagged.
- `SQL_RE` accepts any text that starts with a SQL verb and later contains ` from `:
  `Select the ledger from the list` and `Delete from the register` get **SQL**; the server returns
  nothing.
- `HTML_RE` does not require the closing `>` (`<a href=x` tags) and omits `b`/`i`/`strong`/`em`
  (the server tags `<b>Important</b>` as HTML; the UI does not). `COLOUR_RE` accepts `rgba()`/`hsla()`;
  `isNumber` accepts `0x10` — all different from `ClipTags`.
- The language vocabulary is different: the UI emits `Script`, `C/C++`, `PHP`, `Rust`, `Python`; the
  server emits `shell`, `csharp`, `javascript`, `typescript`, … . `App.tsx:146-149` filters on
  `tag:<key>` (`c-c`, `script`) locally; `ClipSearch` has no `tag:` grammar, so a chip click cannot
  drive `search` (and would become a text term if it did). The server's `tags[]` is ignored by
  `ClipItem`/`normalizeItem` (`host.ts:14-29, 124-140`).

This is the “one home” rule broken in the most expensive way: two classifiers, two answers for the same
clip, and the looser one is the one the owner sees. It is in-progress work, so the fix is a direction,
not a bug report against the audited commit: **delete `tags.ts`; render the server `tags[]` already in
the DTO; make a chip emit `csharp:` / `spreadsheet:` so the same grammar filters it.**

---

## The three changes I would insist on

1. **Make the classifier’s positive tests actually positive.** Fix the TypeScript annotation gate, the
   Python `import`/`from`/`class` shapes, the JavaScript `function (` body, and the SQL DDL terminator.
   Then extend the fixture gate with the prose corpus from this audit — the current 27/27 table has no
   ordinary sentence containing `: any`, `: void`, `import data …`, `function (…)`, or `class X (`.
   Until then “0 false positives” is only true of the fixtures, not of the product.
2. **Never classify a truncated preview as the whole clip.** The store hands `Classify` the first 1,200
   characters (`ClipboardStore.Items.cs:178,196`) and the code applies whole-text rules to them. Gate
   link/email/path/colour/number/JSON on `!item.IsTruncated` (HEAD; `TextLength > PreviewChars` at the
   audited commit); that also avoids the known code-point/UTF-16 `TextLength` trap and needs no
   migration. Add the three preview fixtures.
3. **Make the tag pass cheap and safe at the sizes the app allows.** Chunk `Meta()` at 500 ids
   (47.5 ms/10k vs 206 ms, no 32,767 cliff) and stop treating its failure as cosmetic; run the search
   off the UI thread (the tracker's S3e). Then re-measure the 50 ms budget at 1k with 1,200-char rows,
   including the `GetRecent` load, not just the parser.

## Three things solidly built

1. **`Matches` is genuinely allocation-free and null-safe.** 10,000 calls over 2,000 items allocated
   **0 bytes**; 5,000 tags, a 1 MB body, Devanagari, duplicate/null tags, and a null item all behave;
   and `exclude` beats `include` in every combination tested.
2. **The tag-token gate is disciplined.** `https://`, `C:`, `C:\`, `mailto:`, `tel:+91`, `छवि:`,
   `图片:` and `123:` never become filters; quoted `image:` is literal; an unknown `foo:` falls back to
   text and is surfaced as `unknownTags`; the aliases live in one place **in Core/Host** (the uncommitted
   UI copy is the exception noted above) and `code:` is exactly the nine language tags (proved by
   fixture, no extras).
3. **The conservative whole-text rules do their job on real content.** All 19 prompt cases that the
   frozen contract agrees must be untagged are untagged, the five `excel:`/`sheet:`/`xls:`/`csv:`
   spellings return identical sets, and the 38-row real DB copy produced six sensible tags with zero
   false positives.

---

## Limits of this audit

- The UI is not wired to `search` (`UI\web\src\host.ts` has no method), so this is a core + bridge-shape
  audit, not an end-to-end click test. The uncommitted S3b UI derives tags locally instead (out-of-scope
  note above); the WebView2 reply was replicated through the same camelCase serializer options, not
  observed in a browser.
- The 10k timing is the sum of measured pieces (`Classify` + `Meta` + the tracker's load number), not a
  single `Search` call against a 10k database; the individual numbers are in §6.
- I did not run the full app build or touch `bin\`; the harness compiles the two real files, and the
  real-data pass used a scratch copy.
- `AUDIT-db-logic.md` already records the `TextLength`/UTF-16 P0 and the 531 ms load; this report does
  not re-litigate those, but the truncation finding (F7) and the perf finding (F4) sit on top of them.

**Skipped:** the browser/WebView end-to-end run and the full-app build. **Risk:** the bridge's thread
behaviour and the TS type wiring are proven by reading, not by a live reply.
