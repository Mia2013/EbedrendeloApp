---
name: ui-development-guidelines
description: Core UI development guidelines — MudBlazor-first, no custom CSS unless required, component conventions, accessibility, and dialog/form patterns
---
 
# UI Development Guidelines
 
## 1. MudBlazor-First Rule
 
**Always use MudBlazor components before anything else.** Check whether a native MudBlazor component exists (`MudStack`, `MudButton`, `MudCard`, `MudTable`, etc.) before composing custom HTML.
 
**Priority order:**
1. **MudBlazor component + utility classes** — e.g., `MudStack`, `MudButton`, `MudCard`
2. **Inline styles** (`style="..."`) — only for 1–2 properties on a single element
3. **External CSS** — only for repeatable patterns (animations, keyframes, complex hover effects)
**Rule:** Never write custom CSS to adjust a single element's spacing or alignment. MudBlazor utilities exist for that.
 
### Never mix external frameworks
 
Do not mix Bootstrap or external CSS grids with MudBlazor. Stick to MudBlazor's built-in utility classes exclusively.
 
### ✅ PREFERRED: Use typed MudStack properties
 
```razor
<MudStack Row="true" Justify="Justify.SpaceBetween" AlignItems="AlignItems.Center">
    <MudText Typo="Typo.h6">Title</MudText>
    <MudButton>Action</MudButton>
</MudStack>
```
 
### ❌ AVOID: Chaining flex utility classes
 
```razor
<!-- Do NOT do this -->
<div class="d-flex justify-space-between align-center">...</div>
```
 
---
 
## 2. Layout & Structure (Stack & Grid)
 
### Prefer `MudStack` over native flex utility classes
 
Avoid chaining classes like `d-flex justify-space-between align-center`. Use typed MudStack properties instead with explicit control over spacing and alignment.
 
```razor
<!-- AVOID -->
<div class="d-flex justify-space-between align-center">...</div>
 
<!-- PREFERRED -->
<MudStack Row="true" Justify="Justify.SpaceBetween" AlignItems="AlignItems.Center">
    ...
</MudStack>
```
 
### Grid Layouts
 
For responsive forms and dashboards, exclusively use `MudGrid` and `MudItem` with explicit breakpoints (`xs`, `sm`, `md`, `lg`).
 
```razor
<MudGrid Spacing="3">
    <MudItem xs="12" sm="6" md="4">
        <MudTextField Label="Name" Margin="Margin.Dense" />
    </MudItem>
    <MudItem xs="12" sm="6" md="4">
        <MudTextField Label="Price" Margin="Margin.Dense" />
    </MudItem>
    <MudItem xs="12" md="4">
        <MudSelect Label="Category" Margin="Margin.Dense">
            <MudSelectItem Value="@1">Category 1</MudSelectItem>
        </MudSelect>
    </MudItem>
</MudGrid>
```
 
### Headings with Icons
 
Page and section headings should always feature a `MudIcon` preceding the `MudText` heading within a `Row="true"` `MudStack`, semantically matching the heading's meaning:
 
```razor
<MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2">
    <MudIcon Icon="@Icons.Material.Filled.RestaurantMenu" Color="Color.Primary" Size="Size.Large" />
    <MudText Typo="Typo.h5">Daily Menu Editor</MudText>
</MudStack>
```
 
---
 
## 3. Action, Button & Icon Conventions
 
### Color & Icon Mappings
 
| Action | Component Type | Variant | Color | Icon | Notes |
|--------|---|---|---|---|---|
| **Create / Add** | `MudButton` | Filled | Primary | Add | Usually primary call-to-action |
| **Edit (Labeled)** | `MudButton` | Filled | Info | Edit | Never use Default/Primary |
| **Edit (Icon-only)** | `MudIconButton` | — | Info | Edit | Use in tables/lists |
| **Save** | `MudButton` | Filled | Success | Save | Leftmost in DialogActions |
| **Cancel / Close** | `MudButton` | Filled | Default | Cancel | Not `.Close` |
| **Delete / Destructive** | `MudButton` / `MudIconButton` | Filled | Error | Delete | Requires confirmation! |
| **Refresh / Reload** | `MudIconButton` / `MudButton` | Filled | Default | Refresh | Non-intrusive action |
| **Export / Download** | `MudButton` | Filled | Default | Download | Non-intrusive action |
 
### Button Rules
 
**Variants:** Default to `Variant="Variant.Filled"` across all buttons (including dialogs), unless it represents a low-emphasis link/toggle action (`Variant.Text` or `Variant.Outlined`).
 
**Sizing:**
- Standard actions & dialogs: `Size="Size.Medium"`
- Dense lists, table row action columns: `Size="Size.Small"`
### Save + Cancel Sequence
 
In button groups or `DialogActions`, Save comes first (left) and Cancel comes second (right) in physical DOM order (no CSS flex-reordering):
 
```razor
<DialogActions>
    <MudButton Variant="Variant.Filled" Color="Color.Success" OnClick="@Save">
        Save
    </MudButton>
    <MudButton Variant="Variant.Filled" Color="Color.Default" OnClick="@Cancel">
        Cancel
    </MudButton>
</DialogActions>
```
 
### Destructive Actions Require Confirmation
 
Deleting items directly on click is **strictly prohibited**. Always prompt the user using `IDialogService.ShowMessageBox` or a dedicated confirmation dialog:
 
```csharp
var result = await DialogService.ShowMessageBox(
    "Are you sure?",
    "This action cannot be undone.",
    "Delete", "Cancel"
);
 
if (result.HasValue && result.Value)
{
    await DeleteItem();
    Snackbar.Add("Item deleted successfully.", Severity.Success);
}
```
 
---
 
## 4. Forms & Data Input
 
### Immediate Input
 
For real-time filtering or on-the-fly validation, always set `Immediate="true"` on `MudTextField`:
 
```razor
<MudTextField 
    @bind-Value="searchTerm" 
    Placeholder="Search..."
    Immediate="true"
    OnImmediateTextChanged="@OnSearchChanged" />
```
 
### Clearable Fields
 
Search fields, filters, and optional dropdowns must include `Clearable="true"`:
 
```razor
<MudTextField Placeholder="Search..." Clearable="true" />
<MudSelect Label="Filter" Clearable="true">
    <MudSelectItem Value="@1">Option</MudSelectItem>
</MudSelect>
```
 
### Form Controls
 
Wrap editable forms in `<EditForm>` or `<MudForm>` and bind validation models cleanly:
 
```razor
<EditForm Model="@model" OnValidSubmit="@HandleSubmit">
    <DataAnnotationsValidator />
    <ValidationSummary />
 
    <MudTextField 
        @bind-Value="@model.Name" 
        Label="Name"
        For="@(() => model.Name)" />
 
    <MudButton ButtonType="ButtonType.Submit" Variant="Variant.Filled" Color="Color.Primary">
        Save
    </MudButton>
</EditForm>
```
 
---
 
## 5. Dialog Conventions (`IDialogService`)
 
### Mandatory Dialog Options
 
When opening dialogs, always explicitly define standard window behavior:
 
```csharp
var options = new DialogOptions
{
    CloseButton = true,        // X button in top-right
    FullWidth = true           // Use available width
};
 
await DialogService.ShowAsync<MyDialog>(null, options);
```
 
### Structural Integrity
 
Inside dialog components, only use the official MudBlazor slots: `<TitleContent>`, `<DialogContent>`, and `<DialogActions>`. Do not wrap dialog contents in arbitrary custom outer containers:
 
```razor
<MudDialog>
    <TitleContent>
        <MudText Typo="Typo.h6">Dialog Title</MudText>
    </TitleContent>
    <DialogContent>
        <!-- Main content -->
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@Cancel">Cancel</MudButton>
        <MudButton Variant="Variant.Filled" Color="Color.Primary" OnClick="@Save">Save</MudButton>
    </DialogActions>
</MudDialog>
 
@code {
    [CascadingParameter] MudDialogInstance MudDialog { get; set; }
 
    private async Task Save()
    {
        await MudDialog.CloseAsync(DialogResult.Ok(true));
    }
 
    private void Cancel()
    {
        MudDialog.Cancel();
    }
}
```
 
---
 
## 6. Loading & Async State Handling
 
### Async Action Buttons
 
Always disable the button and show a spinner while an asynchronous operation is running:
 
```razor
<MudButton 
    Variant="Variant.Filled"
    Color="Color.Success"
    StartIcon="@(_isSubmitting ? null : Icons.Material.Filled.Save)"
    Disabled="@_isSubmitting"
    OnClick="@HandleSave">
    @if (_isSubmitting)
    {
        <MudProgressCircular Class="ms-n1" Size="Size.Small" Indeterminate="true" />
        <MudText Class="ms-2">Saving...</MudText>
    }
    else
    {
        <MudText>Save</MudText>
    }
</MudButton>
 
@code {
    private bool _isSubmitting = false;
 
    private async Task HandleSave()
    {
        _isSubmitting = true;
        try
        {
            await SaveAsync();
            Snackbar.Add("Saved successfully!", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add("Save failed. Please try again.", Severity.Error);
        }
        finally
        {
            _isSubmitting = false;
        }
    }
}
```
 
### Data Tables & Lists
 
Never use an `@if (_isLoading)` block to conditionally hide/replace an entire table. Bind `Loading="@_isLoading"` directly to `MudTable` to avoid visual layout jumps:
 
```razor
<MudTable Items="@Items" Loading="@IsLoading">
    <HeaderContent>
        <MudTh>Name</MudTh>
        <MudTh>Price</MudTh>
    </HeaderContent>
    <RowTemplate>
        <MudTd DataLabel="Name">@context.Name</MudTd>
        <MudTd DataLabel="Price">@context.Price Ft</MudTd>
    </RowTemplate>
</MudTable>
```

### Numeric & Money Columns — Right-Align by Default

**Rule:** any table/grid column holding money or another numeric value is right-aligned by default —
digits line up on the ones place, which is what makes a column of numbers scannable. Left/center
alignment for numeric columns is the exception, not the default; text/label columns stay left-aligned
as usual.

Apply it on both the header and the row cell (`MudTh`/`MudTd` accept arbitrary attributes, no custom
CSS class needed — `Style="text-align:right"` is enough per rule 10, it's a single property on a
single element):

```razor
<MudTable Items="@Items" Hover="true" Dense="true">
    <HeaderContent>
        <MudTh>Name</MudTh>
        <MudTh Style="text-align:right">Price</MudTh>
    </HeaderContent>
    <RowTemplate>
        <MudTd DataLabel="Name">@context.Name</MudTd>
        <MudTd DataLabel="Price" Style="text-align:right">@context.Price Ft</MudTd>
    </RowTemplate>
</MudTable>
```

### Numeric & Money Columns — Thousands Separator

**Rule:** any displayed money amount or other large number gets a thousands separator, not just
right-alignment — a bare `1000000` is hard to parse at a glance, `1 000 000` reads instantly.
This is a **UI-only, display-time** rule: it governs what gets rendered on screen, never what gets
persisted. Domain entities and DTOs keep plain `int`/`decimal` values with no separators or grouping
— formatting happens only where the value is written into markup.

In this repo the shared formatter is `EbedrendeloApp.Common.Formatting.HungarianNumberFormat`
(`Number(int)` for a plain grouped number, `Huf(int)` for a grouped amount with `" Ft"` appended,
both with `int?` overloads). It's covered by the `_Imports.razor` global `@using`, so no per-file
`@using` is needed. Never format inline with ad-hoc `ToString("N0")` calls scattered across
components — route every money/number display through this one helper, same reasoning as the
megjelenítési helper pattern in `ebedrendelo-extensions` (one place per concept, not a switch
re-typed in every `.razor` file).

```razor
<MudTd DataLabel="Price" Style="text-align:right">@HungarianNumberFormat.Huf(context.PriceHuf)</MudTd>
<MudTd DataLabel="Count" Style="text-align:right">@HungarianNumberFormat.Number(context.Count)</MudTd>
```

Under the hood this is `value.ToString("N0", CultureInfo.GetCultureInfo("hu-HU"))` — the hu-HU
group separator is U+00A0 (a non-breaking space, not a plain space), which also keeps a number from
wrapping mid-digit at a narrow viewport. Because it's not an ASCII space, a bUnit assertion matching
rendered markup must use the `\u00A0` escape (e.g. `"1\u00A0400 Ft"`), never a literal space typed
in the source — a plain space silently fails to match at runtime.

### Tables with a Date Column — Newest First by Default

**Rule:** a table/list that has a date column defaults to descending date order — most recent first.
Someone opening an order list, a ledger, or an activity log almost always wants to see what just
happened, not scroll past months of history to reach it. Do this in the query layer (`OrderByDescending`
in the MediatR handler that produces the rows), not by re-sorting client-side in the `.razor` file —
keeps the sort next to the `Where` clauses it pairs with, and the UI just renders what it's given.

```csharp
var orders = await db.MenuOrders
    .Where(o => request.UserId == null || o.UserId == request.UserId)
    .OrderByDescending(o => o.Date)
    .ThenByDescending(o => o.Id)   // stable tie-break for same-day rows
    .ToListAsync(cancellationToken);
```

**This is a default, not a blanket override — three real exceptions exist in this repo, and the
same reasoning applies to any new screen:**

1. **Calendar/matrix layouts stay chronological (ascending).** A week grid, a month grid, or a
   date×item matrix (`DailyMenuEditor`, `MyOrders`, à la carte monthly kitchen summary) *is* a
   calendar — Monday belongs above Friday. These aren't sorted record lists at all, so the rule
   doesn't apply.
2. **Forward-looking planning lists stay ascending (soonest first).** `NonOrderableDays` shows
   upcoming exclusions (default range: today → +3 months) — an admin managing what's coming up
   wants the nearest date on top, not the furthest-out one. Judge by what the data represents:
   history → newest first, schedule → soonest first.
3. **An append-only ledger where order encodes cause and effect stays ascending.** See
   `GetMyCreditLedgerHandler.cs` (AC 5.3.3): a cancellation credit must render before the later
   revocation that consumes it, or the running statement reads backwards. When reversing the order
   would make a domain narrative unreadable, don't reverse it — and leave the reasoning in a comment
   the way that handler does, so the exception survives the next refactor.

When adding a new date-bearing list, ask: is this a log of what happened (→ newest first), a
calendar/schedule (→ chronological), or a narrative where each row explains the next (→ chronological)?
Default to newest-first only for the first case.

---
 
## 7. Notifications & Feedback (`ISnackbar`)
 
Provide instant feedback for every backend mutation:
 
- **Success:** `Snackbar.Add("Settings saved!", Severity.Success);`
- **Error:** `Snackbar.Add("Could not save data.", Severity.Error);`
- **Info:** `Snackbar.Add("Data refreshed.", Severity.Info);`
- **Warning:** `Snackbar.Add("Warning: capacity limits reached.", Severity.Warning);`
### Error Sanitization
 
**Never expose raw technical exception messages or stack traces directly to end users.** Log them on the server/console and present friendly UI copy:
 
```csharp
// ❌ AVOID
Snackbar.Add(ex.Message, Severity.Error); 
// Results in: "NullReferenceException: Object reference not set to an instance of an object."
 
// ✅ PREFERRED
Snackbar.Add("Could not load data. Please try again.", Severity.Error);
Logger.LogError(ex, "Error loading data");
```
 
---
 
## 8. No Compound Boolean Logic in Markup
 
**Single-condition ternary is fine:**
- A ternary driven by one piece of state (`@(x ? Color.Primary : Color.Default)`) inline in markup is clean, readable, and permitted.
**Compound logic is prohibited inline:**
- Chaining multiple booleans with `&&`/`||` inside markup attributes (e.g. `Disabled="a || b || c"`) is strictly banned.
**Rule:** Two or more conditions joined by `&&`/`||` must be extracted into a meaningful, named `bool` property or helper method in `@code` (`CanSave`, `CanEdit`, `IsFormValid`), and bound directly:
 
```razor
<!-- AVOID -->
<MudButton Disabled="@(!isLoaded || isSubmitting || !hasPermission)">Save</MudButton>
 
<!-- PREFERRED -->
<MudButton Disabled="@(!CanSubmit)">Save</MudButton>
 
@code {
    private bool CanSubmit => isLoaded && !isSubmitting && hasPermission;
}
```
 
---
 
## 9. Code Example: Theme Utilities (No Custom CSS)
 
Here's an example of using **only** MudBlazor utility classes and no custom CSS:
 
```razor
<!-- Pink Pill example using MudBlazor utility classes exclusively -->
<MudStack Row="true" AlignItems="AlignItems.Center" Justify="Justify.Center" Class="py-4 px-6 mx-4 rounded-pill" style="background-color: var(--mud-palette-secondary);">
    <MudText Align="Align.Center" Color="Color.Surface">Text</MudText>
</MudStack>
```
 
Or with proper MudBlazor styling:
 
```razor
<MudPaper Elevation="0" Class="py-4 px-6 mx-4 rounded-pill mud-background-secondary">
    <MudText Align="Align.Center">Text</MudText>
</MudPaper>
```
 
---
 
## 10. CSS — Exceptional Cases
 
### ✅ When CSS is acceptable
 
CSS should only be written when MudBlazor components and utility classes are strictly insufficient:
- Animations and keyframes
- Complex hover/focus effects not achievable via theme variables or utility classes
- Repeatable patterns that appear across multiple components
### ❌ When to avoid CSS
 
Do not write custom CSS for:
- Adjusting spacing (1–2 properties) — use MudStack spacing or inline styles
- Single element styling — use inline `style="..."`
- When a MudBlazor utility class solves the problem
### Inline styles — acceptable for 1–2 properties
 
```razor
<!-- OK: 1–2 properties on a single element -->
<div style="margin-bottom: 20px; padding: 10px;">
    Content
</div>
 
<!-- NOT OK: multiple custom properties — write CSS instead -->
<div style="margin: 20px; padding: 15px; border-radius: 8px; background: #f5f5f5; box-shadow: 0 1px 3px rgba(0,0,0,0.1);">
    This should have been in a CSS class
</div>
```
 
---
 
## 11. Theme & Styling
 
### Utility Classes
 
MudBlazor includes Bootstrap-compatible utility classes for quick styling:
 
- **Margin:** `m-4`, `mt-2`, `mx-3`, `my-2`
- **Padding:** `p-4`, `pt-2`, `px-3`, `py-2`
- **Display:** `d-none`, `d-flex`, `d-grid`, `d-inline-block`
- **Flexbox:** `justify-center`, `align-center`, `justify-space-between`
- **Text:** `text-center`, `text-right`, `text-capitalize`, `font-weight-bold`
- **Borders:** `rounded`, `rounded-pill`, `rounded-lg`
- **Responsive:** Use breakpoints like `xs-12`, `sm-6`, `md-4`, `lg-3`
### Color Utilities
 
Use color classes directly on components:
 
```razor
<MudButton Color="Color.Primary">Primary</MudButton>
<MudButton Color="Color.Success">Success</MudButton>
<MudText Color="Color.TextSecondary">Muted text</MudText>
```
 
---

## 12. Choosing a Layout: Cards vs. Tables

**Rule:** `MudTable` is the right default for admin/back-office data — lists to scan, sort, bulk-manage,
with the same handful of actions on every row. It is **not** automatically the right default for an
**end-user selection screen** — a page where the user is picking one or more items from a bounded list
(a menu, an à la carte offer, a plan/tier picker). For that, reach for a **grouped card grid**
(`MudGrid` + `MudItem` + `MudPaper`, clickable) before a dense table of rows and tiny action buttons.
A table optimizes for scanning many similar records; cards optimize for browsing and choosing — they
read better, work better on a narrow/mobile viewport (a full-width tappable card is a much easier touch
target than a table-row checkbox or icon button), and make the selected state visually obvious without
needing a dedicated status column.

**Prefer cards when:**
- The user is choosing item(s) from a bounded list, not scanning/managing a record set.
- Each item carries more than 2–3 fields (name, price, allergens, nutrition) that would cramp into
  table cells.
- Mobile/touch use is expected.

**A table still wins when:**
- It's an admin/back-office screen: bulk edit, sort by column, scan dozens/hundreds of rows (e.g.
  `AdminALaCarteItems.razor`, `AdminOrders.razor`).
- It's a dense record list where every row needs the same actions and column-level scanning/sorting
  matters more than browsing — the Numeric & Money Columns and "Tables with a Date Column" rules above
  still apply whenever a table *is* the right choice.

**Pattern — grouped, clickable selection cards** (see `TodayMenu.razor`'s à la carte section for the
real implementation this was extracted from):

```razor
@foreach (var group in Items.GroupBy(i => i.Category))
{
    <MudStack Row AlignItems="AlignItems.Center" Spacing="2" Class="mt-3 mb-2">
        <MudIcon Icon="@CategoryDisplay.Icon(group.Key)" Size="Size.Small" Color="Color.Primary" />
        <MudText Typo="Typo.subtitle1">@CategoryDisplay.Name(group.Key)</MudText>
    </MudStack>
    <MudGrid Spacing="2">
        @foreach (var item in group)
        {
            <MudItem xs="12" sm="6" md="4">
                <MudPaper Outlined="true" Class="pa-3" Style="@CardStyle(item)" Elevation="0"
                          @onclick="@(() => OnCardClick(item))">
                    <!-- name, price, a MudChip for selected/unavailable state -->
                </MudPaper>
            </MudItem>
        }
    </MudGrid>
}
```

Selected/unavailable state lives on the card itself — a `MudChip` (icon + text, e.g. "Selected" /
"Sold out" / "Already ordered") plus at most a one- or two-property `Style` override for a background
tint (`Style="background:var(--mud-palette-primary-hover);"`, within rule 10's "1–2 properties on a
single element" allowance). Never signal selection with color alone — always pair it with an icon or
label, and never add a separate status *column* the way a table would need one.

---
 
## 13. Implementation Checklist
 
### When building components
- [ ] For an end-user selection screen, consider a card grid (rule 12) before reaching for `MudTable`
- [ ] Base layout on `MudStack` or `MudGrid`
- [ ] Async operations: spinner + disabled state
- [ ] Validation: `EditForm` + `DataAnnotationsValidator`
- [ ] Dialogs: `CloseButton="true"`, `FullWidth="true"`
### User interaction
- [ ] Destructive operations require confirmation dialog
- [ ] Snackbar notification after every backend call
- [ ] Error sanitization (never expose raw exceptions)
- [ ] Loading state in UI (spinner, disabled buttons)
 
