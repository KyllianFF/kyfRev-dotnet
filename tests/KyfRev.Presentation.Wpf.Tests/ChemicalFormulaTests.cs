using KyfRev.Presentation.Wpf.Reading;
using WpfMath.Parsers;

namespace KyfRev.Presentation.Wpf.Tests;

/// <summary>L'écriture chimique <c>\ce{…}</c>, traduite pour le moteur de formules.</summary>
[TestClass]
public sealed class ChemicalFormulaTests
{
    [TestMethod]
    [DataRow(@"\ce{H2O}", @"{\mathrm{H}_{2}\mathrm{O}}")]
    [DataRow(@"\ce{2H2 + O2 -> 2H2O}", @"{2\mathrm{H}_{2}\;+\;\mathrm{O}_{2}\; \rightarrow \;2\mathrm{H}_{2}\mathrm{O}}")]
    [DataRow(@"\ce{SO4^2-}", @"{\mathrm{SO}_{4}^{2-}}")]
    [DataRow(@"\ce{Na+ + Cl-}", @"{\mathrm{Na}^{+}\;+\;\mathrm{Cl}^{-}}")]
    [DataRow(@"\ce{Fe^{3+}}", @"{\mathrm{Fe}^{3+}}")]
    [DataRow(@"\ce{Ca(OH)2}", @"{\mathrm{Ca}(\mathrm{OH})_{2}}")]
    [DataRow(@"\ce{CuSO4.5H2O}", @"{\mathrm{CuSO}_{4} \cdot 5\mathrm{H}_{2}\mathrm{O}}")]
    [DataRow(@"\ce{N2 + 3H2 <=> 2NH3}", @"{\mathrm{N}_{2}\;+\;3\mathrm{H}_{2}\; \rightleftharpoons \;2\mathrm{NH}_{3}}")]
    [DataRow(@"x = \ce{CO2} + \frac{1}{2}", @"x = {\mathrm{CO}_{2}} + \frac{1}{2}")]
    public void Chemistry_becomes_ordinary_latex(string source, string expected) =>
        Assert.AreEqual(expected, ChemicalFormula.Expand(source));

    [TestMethod]
    [DataRow(@"E = mc^2")]
    [DataRow(@"\cellule + \cdot")]
    [DataRow(@"\ce{H2O")]
    [DataRow(@"\ce")]
    public void What_is_not_chemistry_does_not_change(string source) =>
        Assert.AreEqual(source, ChemicalFormula.Expand(source));

    [STATestMethod]
    [DataRow(@"\ce{H2O}")]
    [DataRow(@"\ce{2H2 + O2 -> 2H2O}")]
    [DataRow(@"\ce{SO4^2-}")]
    [DataRow(@"\ce{Na+ + Cl- -> NaCl}")]
    [DataRow(@"\ce{Ca(OH)2 + 2HCl -> CaCl2 + 2H2O}")]
    [DataRow(@"\ce{CuSO4.5H2O}")]
    [DataRow(@"\ce{N2 + 3H2 <=> 2NH3}")]
    [DataRow(@"\ce{A <- B <-> C}")]
    [DataRow(@"\ce{[Cu(NH3)4]^2+}")]
    [DataRow(@"\ce{CO2(g) + H2O(l) <=> H2CO3(aq)}")]
    public void The_formula_engine_draws_the_translation(string source)
    {
        // Le moteur refuse ce qu'il ne connaît pas : une traduction qu'il lit se dessinera.
        Assert.IsNotNull(WpfTeXFormulaParser.Instance.Parse(DocumentBuilder.Drawable(source)));
    }
}
