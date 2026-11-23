# DESIGN-spacing-roles — frozen per-role spacing (crisp sprint, pixel-scan evidence 23 Nov)

The single source for margins/paddings by ROLE. Rows/tab-strip/status-bar values remain with the S1
row sprint (design docs); everything else is frozen HERE. Source docs: DESIGN-layout.md rails,
DESIGN-v2-ui.md. Evidence: pixel scans at 150 % DPI (search ink 50.7 DIP vs row ink 52.7 = same 51 rail).

| Role | Value | Where |
|---|---|---|
| Surface rail (content inset) popup/editor | **18** | popup header/tabs/status, editor header/content/footer |
| Surface rail — Settings | **20** | header band, tab strip, 672 column, footer |
| Surface rail — Toast | **14** | toast root |
| Header band height | **48** | popup, settings, editor |
| Icon/tile → text gap | **10** | search, row tile |
| Section gap above a label | **24** | Settings section labels |
| Label → card | **12** | Settings |
| Card inner padding | **14** | Settings cards |
| Row gap inside a card | **12** | Settings rows |
| Content → footer gap | **14** | editor |
| Footer padding | rail × **12** | editor 18,12; Settings 20,12 |
| Button gap in a bar | **8** | all footers/prompt bars |
| Toast header → body | **4** | body + thumb |
| Toast label pair gap | **8** | app → time |
| Close/click target | **24×24, no negative margins** | toast ✕ (mechanically hit-tested) |

Known residues (recorded, not bugs): Settings' centred 672 column lands the rail at 21 DIP (1 DIP
centring residue of 714/672); window corner radius is **8** (DWM offers only its own sizes — the Ortu
14 died with the layered window, accepted trade for ClearType).
