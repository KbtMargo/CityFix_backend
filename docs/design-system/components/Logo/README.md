# Logo
The CivicReport lockup from `components/ui/Logo.tsx`: the 45px bar-chart mark on a #0C141F tile, the word **CivicReport** and the line *Community Platform*.

- The mark is the exact SVG from `Icons.tsx` ("logo"). It is inline, not an `<img>`, so the page controls it. Its tile colour is fixed (`logo-tile`) in both themes.
- In the app the 45px SVG sits in a 40px `rounded-xl` wrapper (`.cf-logo__mark`) with `shadow-md`. The SVG overflows the wrapper by 2.5px on each side. That is how the source renders, so it is kept.
- `.cf-logo` scales to 105% on hover (`hover:scale-105`, 200ms).

**Do**
- Put it at the top of the sidebar and in the landing header.
- Link it to the home page.

**Don't**
- Recolour the mark.
- Set the name in any other weight or tracking than `cf-logo` (20px bold, −0.05em).

```cshtml
<a class="cf-logo" asp-controller="Home" asp-action="Index">
  <span class="cf-logo__mark"><partial name="_LogoMark" /></span>  @* the SVG from assets/Logos/civicreport-mark.svg, inline *@
  <span class="cf-logo__text"><span class="cf-logo__name">CivicReport</span><span class="cf-logo__tag">Community Platform</span></span>
</a>
```
