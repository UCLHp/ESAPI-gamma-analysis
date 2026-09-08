using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Runtime.CompilerServices;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Text;
using System.Reflection;


//[assembly: AssemblyVersion("1.2.0")]
//[assembly: AssemblyFileVersion("1.2.0")]
[assembly: AssemblyInformationalVersion("1.1")]

[assembly: ESAPIScript(IsWriteable = true)]


namespace VMS.TPS
{
    public partial class Script
    {
       
        public void Execute(ScriptContext context, Window window)
        {
            // NOTE: Patient.BeginModifications() is deliberately NOT called here.
            // The gamma analysis itself is read-only, so launching this script must not
            // mark the patient as modified. BeginModifications() is called lazily inside
            // CreateGammaPlan(), which is the only code path that writes to the database.
            // This allows the analysis to be run on read-only / approved patients as long
            // as "Produce Gamma Plot?" is set to "No".
            var gammaUI = new GammaIndexWindow(context);
            window.Content = gammaUI;
            window.Title = "Gamma Index Calculator";
            window.Width = 1000;
            window.Height = 800;
        }
    }

    public partial class GammaIndexWindow : UserControl
    {
        private ScriptContext _context;
        private ComboBox _Grid_APlanCombo;
        private ComboBox _Grid_BPlanCombo;
        private ComboBox _calculationTypeCombo;
        private ComboBox _GammaPlotCombo;
        private TextBox _dtaTextBox;
        private TextBox _ddTextBox;
        private TextBox _minDoseTextBox;
        private TextBlock _resultsTextBlock;
        private Button _calculateButton;

        // Pre-computed lookup tables for performance.
        // These hold the world-coordinate position of each Grid_A voxel index along each
        // axis, for the Grid_A grid (the one we search within).
        //
        // ASSUMPTION: these tables are separable (X position depends only on the X index,
        // etc.), which is only valid when the Grid_A dose grid axes are aligned with the
        // world axes. See PrecomputeGrid_APositionTables() for details. This holds for
        // standard patient orientations, which is the only case exercised so far.
        private double[] _Grid_AXPositions;
        private double[] _Grid_AYPositions;
        private double[] _Grid_AZPositions;

        public GammaIndexWindow(ScriptContext context)
        {
            _context = context;
            InitializeComponent();
            PopulatePlanComboBoxes();
        }

        private void InitializeComponent()
        {
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.Margin = new Thickness(10);

            // Grid_A Plan = Ground Truth
            var Grid_APlanLabel = new Label { Content = "Grid_A Plan:", FontWeight = FontWeights.Bold };
            Grid.SetRow(Grid_APlanLabel, 0);
            mainGrid.Children.Add(Grid_APlanLabel);

            _Grid_APlanCombo = new ComboBox { Margin = new Thickness(0, 5, 0, 10) };
            Grid.SetRow(_Grid_APlanCombo, 1);
            mainGrid.Children.Add(_Grid_APlanCombo);

            // Grid_B Plan = The one being checked
            var Grid_BPlanLabel = new Label { Content = "Grid_B Plan (Normalise wrt this):", FontWeight = FontWeights.Bold };
            Grid.SetRow(Grid_BPlanLabel, 2);
            mainGrid.Children.Add(Grid_BPlanLabel);

            _Grid_BPlanCombo = new ComboBox { Margin = new Thickness(0, 5, 0, 10) };
            Grid.SetRow(_Grid_BPlanCombo, 3);
            mainGrid.Children.Add(_Grid_BPlanCombo);

            var paramGrid = new Grid();
            paramGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            paramGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            paramGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            paramGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            paramGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var calcTypeStack = new StackPanel();
            calcTypeStack.Children.Add(new Label { Content = "Calculation Type:" });
            _calculationTypeCombo = new ComboBox();
            _calculationTypeCombo.Items.Add("Global");
            _calculationTypeCombo.Items.Add("Local");
            _calculationTypeCombo.SelectedIndex = 0;
            calcTypeStack.Children.Add(_calculationTypeCombo);
            Grid.SetColumn(calcTypeStack, 0);
            paramGrid.Children.Add(calcTypeStack);

            var dtaStack = new StackPanel();
            dtaStack.Children.Add(new Label { Content = "DTA (mm):" });
            _dtaTextBox = new TextBox { Text = "3.0" };
            dtaStack.Children.Add(_dtaTextBox);
            Grid.SetColumn(dtaStack, 1);
            paramGrid.Children.Add(dtaStack);

            var ddStack = new StackPanel();
            ddStack.Children.Add(new Label { Content = "DD (%):" });
            _ddTextBox = new TextBox { Text = "3.0" };
            ddStack.Children.Add(_ddTextBox);
            Grid.SetColumn(ddStack, 2);
            paramGrid.Children.Add(ddStack);

            var minDoseStack = new StackPanel();
            minDoseStack.Children.Add(new Label { Content = "Min Dose (%):" });
            _minDoseTextBox = new TextBox { Text = "10.0" };
            minDoseStack.Children.Add(_minDoseTextBox);
            Grid.SetColumn(minDoseStack, 3);
            paramGrid.Children.Add(minDoseStack);

            var GammaPlotStack = new StackPanel();
            GammaPlotStack.Children.Add(new Label { Content = "Produce Gamma Plot?" });
            _GammaPlotCombo = new ComboBox();
            _GammaPlotCombo.Items.Add("Yes");
            _GammaPlotCombo.Items.Add("No");
            _GammaPlotCombo.SelectedIndex = 0;
            GammaPlotStack.Children.Add(_GammaPlotCombo);
            Grid.SetColumn(GammaPlotStack, 4);
            paramGrid.Children.Add(GammaPlotStack);

            Grid.SetRow(paramGrid, 5);
            mainGrid.Children.Add(paramGrid);

            _calculateButton = new Button
            {
                Content = "Calculate Gamma Index",
                Height = 40,
                Margin = new Thickness(0, 20, 0, 10),
                FontWeight = FontWeights.Bold
            };
            _calculateButton.Click += CalculateButton_Click;
            Grid.SetRow(_calculateButton, 6);
            mainGrid.Children.Add(_calculateButton);

            var resultsLabel = new Label { Content = "Results:", FontWeight = FontWeights.Bold };
            Grid.SetRow(resultsLabel, 7);
            mainGrid.Children.Add(resultsLabel);

            _resultsTextBlock = new TextBlock
            {
                Text = "Select plans and click Calculate to see results.",
                Margin = new Thickness(0, 5, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Background = new SolidColorBrush(Colors.LightGray),
                Padding = new Thickness(10),
                FontFamily = new FontFamily("Consolas")
            };
            Grid.SetRow(_resultsTextBlock, 8);
            mainGrid.Children.Add(_resultsTextBlock);
            Content = mainGrid;
            _resultsTextBlock.Text = "Gamma Index Analysis Results\n";
            _resultsTextBlock.Text += CreateTableHeader();
        }

        private void PopulatePlanComboBoxes()
        {
            var course = _context.Course;
            if (course == null) return;

            var plansWithDose = new List<PlanSetup>();

            // ExternalPlanSetup and IonPlanSetup both derive directly from PlanSetup and are
            // siblings, not parent/child, so Course.ExternalPlanSetups does NOT include ion
            // plans. Both collections must be enumerated to cover every modality, and doing
            // so cannot produce duplicate entries.
            //
            // ExternalPlanSetups -> photon / electron plans
            foreach (var plan in course.ExternalPlanSetups)
            {
                if (plan.Dose != null)
                    plansWithDose.Add(plan);
            }

            // IonPlanSetups -> proton (ion) plans
            foreach (var plan in course.IonPlanSetups)
            {
                if (plan.Dose != null)
                    plansWithDose.Add(plan);
            }

            // NOTE: PlanSums are not offered. A PlanSum is a PlanningItem with a Dose but has
            // no TotalDose, which the absolute-dose scaling in ExtractDoseArrayFlat relies on.

            foreach (var plan in plansWithDose)
            {
                _Grid_APlanCombo.Items.Add(plan);
                _Grid_BPlanCombo.Items.Add(plan);
            }

            if (plansWithDose.Count > 0)
            {
                _Grid_APlanCombo.SelectedIndex = 0;
                if (plansWithDose.Count > 1)
                    _Grid_BPlanCombo.SelectedIndex = 1;
            }
        }

        private void CalculateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _calculateButton.IsEnabled = false;
                Mouse.OverrideCursor = Cursors.Wait;
                
                if (string.IsNullOrEmpty(_resultsTextBlock.Text))
                {
                    _resultsTextBlock.Text = "Gamma Index Analysis Results\n";
                    _resultsTextBlock.Text += CreateTableHeader();
                }
                
                var Grid_APlan = _Grid_APlanCombo.SelectedItem as PlanSetup;
                var Grid_BPlan = _Grid_BPlanCombo.SelectedItem as PlanSetup;
                
                if (Grid_APlan == null || Grid_BPlan == null)
                {
                    MessageBox.Show("Please select both Grid_A and Grid_B plans.");
                    return;
                }
                if (Grid_APlan == Grid_BPlan)
                {
                    MessageBox.Show("Grid_A and Grid_B plans must be different.");
                    return;
                }
                if (!double.TryParse(_dtaTextBox.Text, out double dta) || dta <= 0)
                {
                    MessageBox.Show("Please enter a valid DTA value (mm).");
                    return;
                }
                if (!double.TryParse(_ddTextBox.Text, out double dd) || dd <= 0)
                {
                    MessageBox.Show("Please enter a valid DD value (%).");
                    return;
                }
                if (!double.TryParse(_minDoseTextBox.Text, out double minDosePercent) || minDosePercent < 0 || minDosePercent > 100)
                {
                    MessageBox.Show("Please enter a valid minimum dose percentage (0-100%).");
                    return;
                }
                
                bool isGlobal = _calculationTypeCombo.SelectedItem.ToString() == "Global";
                var result = CalculateGammaIndex(Grid_APlan, Grid_BPlan, dta, dd, minDosePercent, isGlobal);

                // Nothing was compared, so there is no meaningful pass rate. Report and
                // abandon this run without writing a results row or creating a gamma plan.
                // The finally block re-enables the UI. Previously accumulated rows are left
                // in place.
                if (result.EvaluatedVoxels == 0)
                {
                    if (result.UncoveredVoxels > 0)
                    {
                        MessageBox.Show(
                            "No voxels could be evaluated.\n\n" +
                            $"Every Grid_B voxel above the {minDosePercent}% dose threshold " +
                            $"({result.UncoveredVoxels:N0} voxels) lies outside the extent of the " +
                            "Grid_A dose grid, so there is no region of space represented by both " +
                            "dose grids at which a comparison can be made.\n\n" +
                            "Check that both plans are on the same image / frame of reference and " +
                            "that their dose grids overlap.",
                            "No Overlapping Region", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else
                    {
                        MessageBox.Show(
                            "No voxels could be evaluated.\n\n" +
                            $"No Grid_B voxel reaches the {minDosePercent}% minimum dose threshold.\n\n" +
                            "Try lowering the minimum dose percentage.",
                            "Nothing to Evaluate", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    return;
                }

                _resultsTextBlock.Text += CreateTableRow(
                    Grid_APlan.Id, 
                    Grid_BPlan.Id, 
                    dta, 
                    dd, 
                    isGlobal ? "Global" : "Local",
                    minDosePercent,
                    result.PassRate,
                    result.Grid_BMaxDose
                );
                
                bool createGammaPlot = _GammaPlotCombo.SelectedItem.ToString() == "Yes";
                if (createGammaPlot)
                {
                    CreateGammaPlan(Grid_APlan, Grid_BPlan, result.GammaArray, result.Grid_BDose, isGlobal, dta, dd, minDosePercent, result.PassRate);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error calculating gamma index: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _resultsTextBlock.Text += $"\nError: {ex.Message}\n";
            }
            finally
            {
                _calculateButton.IsEnabled = true;
                Mouse.OverrideCursor = null;
            }
        }

        private string CreateTableHeader()
        {
            int Grid_APlanWidth = 20;
            int Grid_BPlanWidth = 20;
            int dtaWidth = 8;
            int ddWidth = 8;
            int typeWidth = 8;
            int minDoseWidth = 10;
            int passRateWidth = 10;
            int MaxDoseWidth = 20;
            
            string header = 
                PadRight("Grid_A Plan", Grid_APlanWidth) + " | " +
                PadRight("Grid_B Plan", Grid_BPlanWidth) + " | " +
                PadRight("DTA(mm)", dtaWidth) + " | " +
                PadRight("DD(%)", ddWidth) + " | " +
                PadRight("Type", typeWidth) + " | " +
                PadRight("MinDose%", minDoseWidth) + " | " +
                PadRight("Pass%", passRateWidth) + " | " +
                PadRight("Grid_BMaxDose(Gy)", MaxDoseWidth) + " | ";
            
            int totalWidth = Grid_APlanWidth + Grid_BPlanWidth + dtaWidth + ddWidth + typeWidth + 
                            minDoseWidth + passRateWidth + MaxDoseWidth + (9 * 3);
            header += new string('-', totalWidth) + "\n";
            
            return header;
        }

        private string CreateTableRow(string Grid_APlan, string Grid_BPlan, double dta, double dd, 
                                    string calcType, double minDosePercent, double passRate, double MaxDose)
        {
            int Grid_APlanWidth = 20;
            int Grid_BPlanWidth = 20;
            int dtaWidth = 8;
            int ddWidth = 8;
            int typeWidth = 8;
            int minDoseWidth = 10;
            int passRateWidth = 10;
            int MaxDoseWidth = 20;
           
            string row = 
                PadRight(TruncateString(Grid_APlan, Grid_APlanWidth), Grid_APlanWidth) + " | " +
                PadRight(TruncateString(Grid_BPlan, Grid_BPlanWidth), Grid_BPlanWidth) + " | " +
                PadRight(dta.ToString("F1"), dtaWidth) + " | " +
                PadRight(dd.ToString("F1"), ddWidth) + " | " +
                PadRight(calcType, typeWidth) + " | " +
                PadRight(minDosePercent.ToString("F1"), minDoseWidth) + " | " +
                PadRight(passRate.ToString("F1"), passRateWidth) + " | " + 
                PadRight(MaxDose.ToString("F3"), MaxDoseWidth) + "\n";
            
            return row;
        }

        private string PadRight(string text, int width)
        {
            if (text.Length >= width)
                return text.Substring(0, width);
            return text + new string(' ', width - text.Length);
        }

        private string TruncateString(string text, int maxLength)
        {
            if (text.Length <= maxLength)
                return text;
            return text.Substring(0, maxLength - 3) + "...";
        }


        /// <summary>
        /// Gamma Index Calculation
        /// 
        /// Grid roles as actually implemented:
        /// - Grid_A = the distribution SEARCHED WITHIN. Candidate comparison points are drawn
        ///            from this grid (voxel centres, then interpolated positions).
        /// - Grid_B = the distribution ITERATED THROUGH. One gamma value is produced per
        ///            Grid_B voxel, and the pass rate is the fraction of evaluated Grid_B
        ///            voxels with γ ≤ 1.
        /// 
        /// IMPORTANT — all normalisation is taken from Grid_B, not Grid_A. This matches the
        /// UI label "Grid_B Plan (Normalise wrt this)". Specifically:
        ///   - Global DD criterion  -> Grid_B max dose
        ///   - Local DD criterion   -> the Grid_B dose at the voxel being evaluated
        ///   - Min dose threshold   -> percentage of Grid_B max dose, applied to Grid_B doses
        /// Grid_A's max dose is computed and reported but is not used in any criterion.
        /// 
        /// EVALUATED REGION — a Grid_B voxel is compared only if BOTH of the following hold:
        ///   1. its Grid_B dose is at or above the minimum dose threshold, and
        ///   2. its position falls within the physical extent of the Grid_A dose grid.
        /// Voxels failing either test are excluded from the pass rate entirely rather than
        /// being counted as failures. See the COVERAGE TEST comment in the main loop.
        /// Note that the normalisation quantities above are still taken over the whole of
        /// Grid_B, not just over the evaluated region.
        /// 
        /// Note that many published descriptions and commercial tools normalise the LOCAL
        /// criterion to the reference (searched) distribution instead. Pass rates from this
        /// script will therefore not necessarily agree with such a tool unless that
        /// difference is accounted for.
        /// 
        /// Gamma formula:
        /// γ = min{ sqrt[ (r_Grid_A - r_Grid_B)² / DTA² + (D_Grid_A - D_Grid_B)² / DD² ] }
        /// 
        /// A voxel passes if γ ≤ 1.0
        /// </summary>
        private GammaResult CalculateGammaIndex(PlanSetup Grid_APlan, PlanSetup Grid_BPlan,
            double dta, double dd, double minDosePercent, bool isGlobal)
        {
            // Set dose presentation to relative for both plans, so that
            // Dose.VoxelToDoseValue() below returns a percentage rather than absolute dose.
            Grid_APlan.DoseValuePresentation = DoseValuePresentation.Relative;
            Grid_BPlan.DoseValuePresentation = DoseValuePresentation.Relative;

            // ---------------------------------------------------------------------------
            // QUERY — left as-is deliberately; behaviour is accepted and in routine use.
            //
            // TotalDose is read AFTER DoseValuePresentation has been switched to Relative.
            // PlanSetup.TotalDose honours the current presentation, so it is expected to
            // report 100 "%" here rather than the prescription in Gy. ExtractDoseArrayFlat
            // uses this as "scaleFactor = planDose / 100.0", which therefore evaluates to 1.0
            // and leaves both dose arrays in percent-of-that-plan's-own-prescription rather
            // than absolute Gy.
            //
            // Consequences to be aware of before changing anything:
            //   - Comparisons are like-for-like only when both plans share the same
            //     prescription. Two plans with different prescriptions are compared
            //     percentage-against-percentage, not Gy-against-Gy.
            //   - The results table column is headed "Grid_BMaxDose(Gy)" and the summary text
            //     prints TotalDose.UnitAsString, so the reported unit may not be Gy.
            //   - Nothing checks that the two plans report the same unit (Gy vs cGy).
            //
            // To verify on site: print Grid_APlan.TotalDose.Dose and .UnitAsString for a plan
            // whose prescription is known. If it reports 100 and "%", the above applies.
            // ---------------------------------------------------------------------------
            var Grid_ATotalDose = Grid_APlan.TotalDose;
            var Grid_BTotalDose = Grid_BPlan.TotalDose;
            var Grid_ADose = Grid_APlan.Dose;
            var Grid_BDose = Grid_BPlan.Dose;

            // Get grid properties for both dose distributions
            var Grid_AProps = GetDoseGridProperties(Grid_ADose);
            var Grid_BProps = GetDoseGridProperties(Grid_BDose);

            // Extract dose arrays as flat 1D arrays for better cache performance
            var Grid_ADoseArray = ExtractDoseArrayFlat(Grid_ADose, Grid_ATotalDose.Dose, Grid_AProps, out double Grid_AMaxDose);
            var Grid_BDoseArray = ExtractDoseArrayFlat(Grid_BDose, Grid_BTotalDose.Dose, Grid_BProps, out double Grid_BMaxDose);

            // For global gamma, the DD criterion is normalised to the Grid_B max dose
            // (i.e. the plan selected as "Normalise wrt this" in the UI), not Grid_A.
            double globalMaxDose = Grid_BMaxDose;

            // Minimum dose threshold is a percentage of the Grid_B max dose, and is applied
            // below to Grid_B voxel doses to decide which voxels are evaluated at all.
            double minDoseThreshold = (minDosePercent / 100.0) * Grid_BMaxDose;

            // Pre-compute position lookup tables for Grid_A grid (the one we search within)
            PrecomputeGrid_APositionTables(Grid_AProps);

            // Pre-compute squared values to avoid repeated calculations
            double dtaSquared = dta * dta;
            double ddFraction = dd / 100.0;

            // Grid_B grid dimensions (the one we iterate through)
            int Grid_BXSize = Grid_BProps.XSize;
            int Grid_BYSize = Grid_BProps.YSize;
            int Grid_BZSize = Grid_BProps.ZSize;
            int Grid_BXYSize = Grid_BXSize * Grid_BYSize;

            // Grid_A grid dimensions (the one we search within)
            int Grid_AXSize = Grid_AProps.XSize;
            int Grid_AYSize = Grid_AProps.YSize;
            int Grid_AZSize = Grid_AProps.ZSize;
            int Grid_AXYSize = Grid_AXSize * Grid_AYSize;

            // Gamma results array - same size as Grid_B grid
            var gammaArrayFlat = new double[Grid_BXSize * Grid_BYSize * Grid_BZSize];

            // Initialise to the sentinel value -1, meaning "not evaluated".
            // A voxel keeps this value for either of two reasons:
            //   - its Grid_B dose is below minDoseThreshold, or
            //   - it falls outside the extent of the Grid_A dose grid (see COVERAGE TEST).
            // Both are excluded from totalVoxels and passedVoxels, and both are mapped to a
            // gamma of 0 when the gamma plot is written (see CreateGammaPlan). The plot does
            // not distinguish the two cases.
            for (int i = 0; i < gammaArrayFlat.Length; i++)
                gammaArrayFlat[i] = -1.0;

            // Calculate search radius based on DTA and Grid_A voxel size.
            //
            // One DTA is sufficient to decide PASS/FAIL: beyond that distance the spatial
            // term alone gives dist²/DTA² > 1, so γ > 1 regardless of dose agreement.
            //
            // It is NOT sufficient to report the true minimum gamma for a FAILING voxel,
            // whose global minimum may lie outside the DTA sphere. Gamma values above 1 in
            // the results and in the gamma plot should therefore be treated as a lower
            // bound, not an exact figure. The pass rate is unaffected.
            double minGrid_ARes = Math.Min(Grid_AProps.XRes, Math.Min(Grid_AProps.YRes, Grid_AProps.ZRes));
            int searchRadius = Math.Max(1, (int)Math.Ceiling(dta / minGrid_ARes));

            int totalVoxels = 0;
            int passedVoxels = 0;

            // Grid_B voxels that are above the dose threshold but fall outside the physical
            // extent of the Grid_A dose grid. Grid_A holds no data at these positions, so no
            // comparison is possible and they are excluded from the statistics entirely
            // (neither passedVoxels nor totalVoxels). Counted here for reporting only.
            int uncoveredVoxels = 0;

            // Pre-compute Grid_B grid directions for position calculation
            double Grid_BXDirX = Grid_BProps.XRes * Grid_BProps.XDirection.x;
            double Grid_BXDirY = Grid_BProps.XRes * Grid_BProps.XDirection.y;
            double Grid_BXDirZ = Grid_BProps.XRes * Grid_BProps.XDirection.z;
            double Grid_BYDirX = Grid_BProps.YRes * Grid_BProps.YDirection.x;
            double Grid_BYDirY = Grid_BProps.YRes * Grid_BProps.YDirection.y;
            double Grid_BYDirZ = Grid_BProps.YRes * Grid_BProps.YDirection.z;
            double Grid_BZDirX = Grid_BProps.ZRes * Grid_BProps.ZDirection.x;
            double Grid_BZDirY = Grid_BProps.ZRes * Grid_BProps.ZDirection.y;
            double Grid_BZDirZ = Grid_BProps.ZRes * Grid_BProps.ZDirection.z;
            double Grid_BOriginX = Grid_BProps.Origin.x;
            double Grid_BOriginY = Grid_BProps.Origin.y;
            double Grid_BOriginZ = Grid_BProps.Origin.z;

            // Pre-compute Grid_A grid inverse transform for position-to-voxel conversion
            double invGrid_AXRes = 1.0 / Grid_AProps.XRes;
            double invGrid_AYRes = 1.0 / Grid_AProps.YRes;
            double invGrid_AZRes = 1.0 / Grid_AProps.ZRes;
            double Grid_AOriginX = Grid_AProps.Origin.x;
            double Grid_AOriginY = Grid_AProps.Origin.y;
            double Grid_AOriginZ = Grid_AProps.Origin.z;
            double Grid_AXDirX = Grid_AProps.XDirection.x;
            double Grid_AXDirY = Grid_AProps.XDirection.y;
            double Grid_AXDirZ = Grid_AProps.XDirection.z;
            double Grid_AYDirX = Grid_AProps.YDirection.x;
            double Grid_AYDirY = Grid_AProps.YDirection.y;
            double Grid_AYDirZ = Grid_AProps.YDirection.z;
            double Grid_AZDirX = Grid_AProps.ZDirection.x;
            double Grid_AZDirY = Grid_AProps.ZDirection.y;
            double Grid_AZDirZ = Grid_AProps.ZDirection.z;

            // Main loop - iterate through Grid_B voxels
            // For each Grid_B voxel, search the Grid_A grid for best gamma
            for (int z = 0; z < Grid_BZSize; z++)
            {
                double zContribX = Grid_BOriginX + z * Grid_BZDirX;
                double zContribY = Grid_BOriginY + z * Grid_BZDirY;
                double zContribZ = Grid_BOriginZ + z * Grid_BZDirZ;

                for (int y = 0; y < Grid_BYSize; y++)
                {
                    double yzContribX = zContribX + y * Grid_BYDirX;
                    double yzContribY = zContribY + y * Grid_BYDirY;
                    double yzContribZ = zContribZ + y * Grid_BYDirZ;

                    for (int x = 0; x < Grid_BXSize; x++)
                    {
                        int Grid_BIdx = x + y * Grid_BXSize + z * Grid_BXYSize;
                        double Grid_BDoseValue = Grid_BDoseArray[Grid_BIdx];

                        // Skip voxels below threshold.
                        // Both the value tested and the threshold derive from Grid_B
                        // (minDoseThreshold = minDosePercent% of Grid_BMaxDose).
                        if (Grid_BDoseValue < minDoseThreshold)
                            continue;

                        // Calculate Grid_B voxel position in world coordinates
                        double Grid_BPosX = yzContribX + x * Grid_BXDirX;
                        double Grid_BPosY = yzContribY + x * Grid_BXDirY;
                        double Grid_BPosZ = yzContribZ + x * Grid_BXDirZ;

                        // Convert Grid_B position to Grid_A voxel coordinates
                        double relX = Grid_BPosX - Grid_AOriginX;
                        double relY = Grid_BPosY - Grid_AOriginY;
                        double relZ = Grid_BPosZ - Grid_AOriginZ;

                        int Grid_ACenterX = (int)Math.Round((relX * Grid_AXDirX + relY * Grid_AXDirY + relZ * Grid_AXDirZ) * invGrid_AXRes);
                        int Grid_ACenterY = (int)Math.Round((relX * Grid_AYDirX + relY * Grid_AYDirY + relZ * Grid_AYDirZ) * invGrid_AYRes);
                        int Grid_ACenterZ = (int)Math.Round((relX * Grid_AZDirX + relY * Grid_AZDirY + relZ * Grid_AZDirZ) * invGrid_AZRes);

                        // ---------------------------------------------------------------
                        // COVERAGE TEST — restrict the comparison to the region of physical
                        // space represented by BOTH dose grids.
                        //
                        // The two grids need no alignment step: Dose.Origin is a DICOM
                        // coordinate, so the mapping above already relates them through the
                        // shared patient frame. Grids of different sizes, resolutions,
                        // origins and extents are all handled correctly by that mapping.
                        //
                        // What the mapping cannot do is invent Grid_A data where the Grid_A
                        // matrix does not reach. Every Grid_B voxel is inside Grid_B by
                        // definition, so testing "inside Grid_A" is sufficient to enforce the
                        // intersection of the two grids, whichever way round the user selects
                        // the plans.
                        //
                        // This test is deliberately placed BEFORE totalVoxels++ so that
                        // uncovered voxels do not enter the pass-rate denominator. Previously
                        // the increment happened first and the search bounds below were merely
                        // clamped to the Grid_A array, so an uncovered voxel was compared
                        // against the nearest Grid_A edge, always failed, and silently
                        // depressed the pass rate in proportion to the uncovered fraction.
                        //
                        // Coverage is GEOMETRIC ONLY — it asks whether the Grid_A matrix
                        // extends to this position, not whether the dose there is non-zero.
                        // A voxel with high dose in one grid and zero in the other is a real
                        // disagreement and must still be allowed to fail.
                        //
                        // Note that Grid_ACenter* comes from Math.Round, so a position lying
                        // up to half a voxel beyond the last Grid_A voxel centre still rounds
                        // into range. That is the intended nearest-voxel behaviour.
                        //
                        // Accepted limitation: a voxel inside Grid_A but within one DTA of its
                        // boundary has part of its search sphere outside the Grid_A data, so
                        // its gamma is biased high. Such voxels are treated as ordinary voxels
                        // here. The bias is one-directional (a truncated search can hide a
                        // pass but never invent one) and the affected shell is thin, so this
                        // is accepted as the simpler and less error-prone behaviour.
                        // ---------------------------------------------------------------
                        if (Grid_ACenterX < 0 || Grid_ACenterX >= Grid_AXSize ||
                            Grid_ACenterY < 0 || Grid_ACenterY >= Grid_AYSize ||
                            Grid_ACenterZ < 0 || Grid_ACenterZ >= Grid_AZSize)
                        {
                            uncoveredVoxels++;
                            continue;
                        }

                        totalVoxels++;

                        // Calculate dose difference criterion.
                        // For global: normalise to the Grid_B max dose (globalMaxDose above).
                        // For local:  normalise to the Grid_B dose at this voxel.
                        // Both branches normalise to Grid_B; Grid_A is never used here.
                        //
                        // NOTE: the local variable is named "ddGrid_A" but holds a Grid_B
                        // quantity in both branches. The name is misleading and is retained
                        // only to avoid touching working code.
                        double ddGrid_A = isGlobal ? globalMaxDose : Grid_BDoseValue;
                        double ddAbsolute = ddFraction * ddGrid_A;
                        double ddAbsoluteSquared = ddAbsolute * ddAbsolute;

                        double minGammaSquared = double.MaxValue;
                        bool foundPass = false;

                        // Define search bounds in Grid_A grid
                        int zMin = Math.Max(0, Grid_ACenterZ - searchRadius);
                        int zMax = Math.Min(Grid_AZSize - 1, Grid_ACenterZ + searchRadius);
                        int yMin = Math.Max(0, Grid_ACenterY - searchRadius);
                        int yMax = Math.Min(Grid_AYSize - 1, Grid_ACenterY + searchRadius);
                        int xMin = Math.Max(0, Grid_ACenterX - searchRadius);
                        int xMax = Math.Min(Grid_AXSize - 1, Grid_ACenterX + searchRadius);

                        // Phase 1: Search Grid_A voxel centers
                        for (int rz = zMin; rz <= zMax && !foundPass; rz++)
                        {
                            double Grid_APosZ = _Grid_AZPositions[rz];
                            double distZ = Grid_APosZ - Grid_BPosZ;
                            double distZSquared = distZ * distZ;

                            if (distZSquared > dtaSquared)
                                continue;

                            for (int ry = yMin; ry <= yMax && !foundPass; ry++)
                            {
                                double Grid_APosY = _Grid_AYPositions[ry];
                                double distY = Grid_APosY - Grid_BPosY;
                                double distYZSquared = distY * distY + distZSquared;

                                if (distYZSquared > dtaSquared)
                                    continue;

                                for (int rx = xMin; rx <= xMax; rx++)
                                {
                                    int Grid_AIdx = rx + ry * Grid_AXSize + rz * Grid_AXYSize;
                                    double Grid_ADoseValue = Grid_ADoseArray[Grid_AIdx];

                                    double Grid_APosX = _Grid_AXPositions[rx];
                                    double distX = Grid_APosX - Grid_BPosX;
                                    double distSquared = distX * distX + distYZSquared;

                                    // Gamma formula: γ² = (distance/DTA)² + (doseDiff/DD)²
                                    double doseDiff = Grid_ADoseValue - Grid_BDoseValue;
                                    double doseDiffSquared = doseDiff * doseDiff;

                                    double gammaSquared = distSquared / dtaSquared + doseDiffSquared / ddAbsoluteSquared;

                                    if (gammaSquared < minGammaSquared)
                                    {
                                        minGammaSquared = gammaSquared;
                                        if (gammaSquared <= 1.0)
                                        {
                                            foundPass = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        // Phase 2: If voxel center search didn't find a pass, try 1D edge interpolation
                        if (!foundPass && minGammaSquared > 1.0)
                        {
                            double interpolatedGammaSquared = GetBestGammaSquared_EdgeInterpolation(
                                Grid_BPosX, Grid_BPosY, Grid_BPosZ, Grid_BDoseValue,
                                Grid_ACenterX, Grid_ACenterY, Grid_ACenterZ,
                                Grid_ADoseArray, Grid_AXSize, Grid_AYSize, Grid_AZSize, Grid_AXYSize,
                                dtaSquared, ddAbsoluteSquared);

                            if (interpolatedGammaSquared < minGammaSquared)
                                minGammaSquared = interpolatedGammaSquared;
                        }

                        // Phase 3: If still not passing, try full 3D trilinear interpolation
                        if (!foundPass && minGammaSquared > 1.0)
                        {
                            double interpolatedGammaSquared = GetBestGammaSquared_TrilinearInterpolation(
                                Grid_BPosX, Grid_BPosY, Grid_BPosZ, Grid_BDoseValue,
                                Grid_ACenterX, Grid_ACenterY, Grid_ACenterZ,
                                Grid_ADoseArray, Grid_AXSize, Grid_AYSize, Grid_AZSize, Grid_AXYSize,
                                dtaSquared, ddAbsoluteSquared);

                            if (interpolatedGammaSquared < minGammaSquared)
                                minGammaSquared = interpolatedGammaSquared;
                        }

                        // Store final gamma value.
                        //
                        // The coverage test above guarantees Grid_ACenter* is a valid index,
                        // so the search bounds below are never degenerate and at least one
                        // Grid_A candidate is always examined. minGammaSquared can therefore
                        // no longer fall through as double.MaxValue.
                        gammaArrayFlat[Grid_BIdx] = Math.Sqrt(minGammaSquared);

                        if (minGammaSquared <= 1.0)
                            passedVoxels++;
                    }
                }
            }

            // Convert flat array back to 3D for compatibility with CreateGammaPlan
            var gammaArray = new double[Grid_BXSize, Grid_BYSize, Grid_BZSize];
            for (int z = 0; z < Grid_BZSize; z++)
            {
                for (int y = 0; y < Grid_BYSize; y++)
                {
                    for (int x = 0; x < Grid_BXSize; x++)
                    {
                        gammaArray[x, y, z] = gammaArrayFlat[x + y * Grid_BXSize + z * Grid_BXYSize];
                    }
                }
            }

            double passRate = totalVoxels > 0 ? (passedVoxels / (double)totalVoxels) * 100.0 : 0.0;
            
            // NOTE: resultText is assigned to GammaResult.ResultText below but is never read
            // or displayed anywhere in the script. The UI writes a row via CreateTableRow()
            // instead. It is retained as a convenient place to hold the detailed breakdown
            // should richer reporting be wired up later.
            string resultText = $"Gamma Analysis Results:\n" +
                                $"Grid_A Plan (searched within): {Grid_APlan.Id}\n" +
                                $"Grid_B Plan (iterated, normalised to): {Grid_BPlan.Id}\n" +
                                $"DTA: {dta} mm\n" +
                                $"DD: {dd}% ({(isGlobal ? "Global" : "Local")})\n" +
                                $"Min Dose Threshold: {minDosePercent}% ({minDoseThreshold:F2} {Grid_ATotalDose.UnitAsString})\n" +
                                $"Evaluated Grid_B Voxels: {totalVoxels:N0}\n" +
                                $"Excluded (outside Grid_A extent): {uncoveredVoxels:N0}\n" +
                                $"Passed Voxels (γ ≤ 1): {passedVoxels:N0}\n" +
                                $"Pass Rate: {passRate:F1}%\n" +
                                $"Grid_A Max Dose: {Grid_AMaxDose:F2} {Grid_ATotalDose.UnitAsString}\n" +
                                $"Grid_B Max Dose: {Grid_BMaxDose:F2} {Grid_BTotalDose.UnitAsString}";

            return new GammaResult
            {
                ResultText = resultText,
                GammaArray = gammaArray,
                Grid_BDose = Grid_BDose,
                PassRate = passRate,
                Grid_BMaxDose = Grid_BMaxDose,
                EvaluatedVoxels = totalVoxels,
                UncoveredVoxels = uncoveredVoxels
            };
        }

        /// <summary>
        /// Pre-compute position lookup tables for the Grid_A grid
        /// (the grid we search within to find best gamma match)
        /// 
        /// ASSUMPTION — the Grid_A dose grid is axis-aligned with the world axes.
        /// 
        /// Each table below uses only the DIAGONAL component of the corresponding direction
        /// cosine (XDirection.x, YDirection.y, ZDirection.z) and ignores the off-diagonal
        /// terms. That is exact when the direction cosine matrix is diagonal, i.e. each
        /// direction vector is (±1,0,0), (0,±1,0), (0,0,±1). This is the case for standard
        /// patient orientations (HFS, HFP, FFS, FFP), which is all that has been used.
        /// 
        /// Be aware that this is INCONSISTENT with the rest of CalculateGammaIndex, which
        /// uses the full three-component direction vectors when computing Grid_B world
        /// positions and when mapping a world position back to Grid_A voxel indices. For a
        /// rotated Grid_A grid the two would disagree and the searched positions would be
        /// wrong. There is currently no check that the grid is axis-aligned.
        /// 
        /// A separable lookup table (X position depending only on the X index) cannot
        /// represent a rotated grid at all, so supporting one would require a full affine
        /// transform rather than a fix to these three loops.
        /// </summary>
        private void PrecomputeGrid_APositionTables(DoseGridProperties props)
        {
            _Grid_AXPositions = new double[props.XSize];
            _Grid_AYPositions = new double[props.YSize];
            _Grid_AZPositions = new double[props.ZSize];

            for (int i = 0; i < props.XSize; i++)
                _Grid_AXPositions[i] = props.Origin.x + i * props.XRes * props.XDirection.x;

            for (int i = 0; i < props.YSize; i++)
                _Grid_AYPositions[i] = props.Origin.y + i * props.YRes * props.YDirection.y;

            for (int i = 0; i < props.ZSize; i++)
                _Grid_AZPositions[i] = props.Origin.z + i * props.ZRes * props.ZDirection.z;
        }

        /// <summary>
        /// Extract dose array as flat 1D array for better cache performance.
        /// 
        /// The intent of "scaleFactor = planDose / 100.0" is to convert VoxelToDoseValue's
        /// percentage output into absolute dose. Note that the caller reads planDose from
        /// PlanSetup.TotalDose AFTER setting DoseValuePresentation to Relative, so planDose
        /// is expected to be 100 and scaleFactor 1.0 — leaving the returned array (and the
        /// maxDose out-parameter) in percent of that plan's own prescription. See the
        /// detailed QUERY comment in CalculateGammaIndex before changing this.
        /// 
        /// Whatever the units, both grids are extracted through this same method, so the
        /// arrays are at least mutually consistent when the two plans share a prescription.
        /// </summary>
        private double[] ExtractDoseArrayFlat(Dose dose, double planDose, DoseGridProperties props, out double maxDose)
        {
            int totalSize = props.XSize * props.YSize * props.ZSize;
            var doseArray = new double[totalSize];
            maxDose = 0;

            double scaleFactor = planDose / 100.0;

            for (int z = 0; z < props.ZSize; z++)
            {
                var plane = new int[props.XSize, props.YSize];
                dose.GetVoxels(z, plane);

                int zOffset = z * props.XSize * props.YSize;

                for (int y = 0; y < props.YSize; y++)
                {
                    int yOffset = y * props.XSize;
                    for (int x = 0; x < props.XSize; x++)
                    {
                        double doseVal = dose.VoxelToDoseValue(plane[x, y]).Dose * scaleFactor;
                        int idx = x + yOffset + zOffset;
                        doseArray[idx] = doseVal;
                        if (doseVal > maxDose)
                            maxDose = doseVal;
                    }
                }
            }

            return doseArray;
        }

        /// <summary>
        /// Phase 2: 1D edge interpolation along axis-aligned directions
        /// 
        /// Searches along the 6 edges connecting the anchor voxel to its face neighbors
        /// Uses analytical solution to find optimal interpolation point
        /// </summary>
        private double GetBestGammaSquared_EdgeInterpolation(
            double Grid_BPosX, double Grid_BPosY, double Grid_BPosZ, double Grid_BDose,
            int rx, int ry, int rz,
            double[] Grid_ADoseArray,
            int Grid_AXSize, int Grid_AYSize, int Grid_AZSize, int Grid_AXYSize,
            double dtaSquared, double ddAbsoluteSquared)
        {
            double bestGammaSquared = double.MaxValue;

            // Clamp center voxel to valid range.
            // Retained as a defensive measure only. Since the COVERAGE TEST in
            // CalculateGammaIndex now rejects any Grid_B voxel whose Grid_A index is out of
            // range, these clamps are no-ops on the current call path. Before that test
            // existed this clamp was reached for uncovered voxels and silently compared them
            // against the nearest Grid_A edge, sometimes an arbitrarily large distance away.
            rx = Math.Max(0, Math.Min(rx, Grid_AXSize - 1));
            ry = Math.Max(0, Math.Min(ry, Grid_AYSize - 1));
            rz = Math.Max(0, Math.Min(rz, Grid_AZSize - 1));

            int anchorIdx = rx + ry * Grid_AXSize + rz * Grid_AXYSize;
            double Da = Grid_ADoseArray[anchorIdx];

            double anchorPosX = _Grid_AXPositions[rx];
            double anchorPosY = _Grid_AYPositions[ry];
            double anchorPosZ = _Grid_AZPositions[rz];

            // Check 6 face neighbors
            // Direction: +X
            if (rx + 1 < Grid_AXSize)
            {
                int ix = rx + 1;
                int interpIdx = ix + ry * Grid_AXSize + rz * Grid_AXYSize;
                double Di = Grid_ADoseArray[interpIdx];
                double deltaD = Di - Da;

                if (Math.Abs(deltaD) > 1e-10)
                {
                    double interpPosX = _Grid_AXPositions[ix];
                    double edgeDx = interpPosX - anchorPosX;
                    double deltaX = Math.Abs(edgeDx);

                    double gammaSquared = CalculateInterpolatedGammaSquared(
                        Grid_BPosX, Grid_BPosY, Grid_BPosZ, Grid_BDose,
                        anchorPosX, anchorPosY, anchorPosZ, Da,
                        interpPosX, anchorPosY, anchorPosZ, Di,
                        deltaD, deltaX, dtaSquared, ddAbsoluteSquared);

                    if (gammaSquared < bestGammaSquared)
                    {
                        bestGammaSquared = gammaSquared;
                        if (gammaSquared <= 1.0) return gammaSquared;
                    }
                }
            }

            // Direction: +Y
            if (ry + 1 < Grid_AYSize)
            {
                int iy = ry + 1;
                int interpIdx = rx + iy * Grid_AXSize + rz * Grid_AXYSize;
                double Di = Grid_ADoseArray[interpIdx];
                double deltaD = Di - Da;

                if (Math.Abs(deltaD) > 1e-10)
                {
                    double interpPosY = _Grid_AYPositions[iy];
                    double edgeDy = interpPosY - anchorPosY;
                    double deltaX = Math.Abs(edgeDy);

                    double gammaSquared = CalculateInterpolatedGammaSquared(
                        Grid_BPosX, Grid_BPosY, Grid_BPosZ, Grid_BDose,
                        anchorPosX, anchorPosY, anchorPosZ, Da,
                        anchorPosX, interpPosY, anchorPosZ, Di,
                        deltaD, deltaX, dtaSquared, ddAbsoluteSquared);

                    if (gammaSquared < bestGammaSquared)
                    {
                        bestGammaSquared = gammaSquared;
                        if (gammaSquared <= 1.0) return gammaSquared;
                    }
                }
            }

            // Direction: +Z
            if (rz + 1 < Grid_AZSize)
            {
                int iz = rz + 1;
                int interpIdx = rx + ry * Grid_AXSize + iz * Grid_AXYSize;
                double Di = Grid_ADoseArray[interpIdx];
                double deltaD = Di - Da;

                if (Math.Abs(deltaD) > 1e-10)
                {
                    double interpPosZ = _Grid_AZPositions[iz];
                    double edgeDz = interpPosZ - anchorPosZ;
                    double deltaX = Math.Abs(edgeDz);

                    double gammaSquared = CalculateInterpolatedGammaSquared(
                        Grid_BPosX, Grid_BPosY, Grid_BPosZ, Grid_BDose,
                        anchorPosX, anchorPosY, anchorPosZ, Da,
                        anchorPosX, anchorPosY, interpPosZ, Di,
                        deltaD, deltaX, dtaSquared, ddAbsoluteSquared);

                    if (gammaSquared < bestGammaSquared)
                    {
                        bestGammaSquared = gammaSquared;
                        if (gammaSquared <= 1.0) return gammaSquared;
                    }
                }
            }

            // Direction: -X
            if (rx - 1 >= 0)
            {
                int ix = rx - 1;
                int interpIdx = ix + ry * Grid_AXSize + rz * Grid_AXYSize;
                double Di = Grid_ADoseArray[interpIdx];
                double deltaD = Di - Da;

                if (Math.Abs(deltaD) > 1e-10)
                {
                    double interpPosX = _Grid_AXPositions[ix];
                    double edgeDx = interpPosX - anchorPosX;
                    double deltaX = Math.Abs(edgeDx);

                    double gammaSquared = CalculateInterpolatedGammaSquared(
                        Grid_BPosX, Grid_BPosY, Grid_BPosZ, Grid_BDose,
                        anchorPosX, anchorPosY, anchorPosZ, Da,
                        interpPosX, anchorPosY, anchorPosZ, Di,
                        deltaD, deltaX, dtaSquared, ddAbsoluteSquared);

                    if (gammaSquared < bestGammaSquared)
                    {
                        bestGammaSquared = gammaSquared;
                        if (gammaSquared <= 1.0) return gammaSquared;
                    }
                }
            }

            // Direction: -Y
            if (ry - 1 >= 0)
            {
                int iy = ry - 1;
                int interpIdx = rx + iy * Grid_AXSize + rz * Grid_AXYSize;
                double Di = Grid_ADoseArray[interpIdx];
                double deltaD = Di - Da;

                if (Math.Abs(deltaD) > 1e-10)
                {
                    double interpPosY = _Grid_AYPositions[iy];
                    double edgeDy = interpPosY - anchorPosY;
                    double deltaX = Math.Abs(edgeDy);

                    double gammaSquared = CalculateInterpolatedGammaSquared(
                        Grid_BPosX, Grid_BPosY, Grid_BPosZ, Grid_BDose,
                        anchorPosX, anchorPosY, anchorPosZ, Da,
                        anchorPosX, interpPosY, anchorPosZ, Di,
                        deltaD, deltaX, dtaSquared, ddAbsoluteSquared);

                    if (gammaSquared < bestGammaSquared)
                    {
                        bestGammaSquared = gammaSquared;
                        if (gammaSquared <= 1.0) return gammaSquared;
                    }
                }
            }

            // Direction: -Z
            if (rz - 1 >= 0)
            {
                int iz = rz - 1;
                int interpIdx = rx + ry * Grid_AXSize + iz * Grid_AXYSize;
                double Di = Grid_ADoseArray[interpIdx];
                double deltaD = Di - Da;

                if (Math.Abs(deltaD) > 1e-10)
                {
                    double interpPosZ = _Grid_AZPositions[iz];
                    double edgeDz = interpPosZ - anchorPosZ;
                    double deltaX = Math.Abs(edgeDz);

                    double gammaSquared = CalculateInterpolatedGammaSquared(
                        Grid_BPosX, Grid_BPosY, Grid_BPosZ, Grid_BDose,
                        anchorPosX, anchorPosY, anchorPosZ, Da,
                        anchorPosX, anchorPosY, interpPosZ, Di,
                        deltaD, deltaX, dtaSquared, ddAbsoluteSquared);

                    if (gammaSquared < bestGammaSquared)
                    {
                        bestGammaSquared = gammaSquared;
                        if (gammaSquared <= 1.0) return gammaSquared;
                    }
                }
            }

            return bestGammaSquared;
        }


        /// <summary>
        /// Phase 3: Full 3D trilinear interpolation search
        /// 
        /// Performs exhaustive search with trilinear interpolation to find minimum gamma.
        /// Samples at step size = voxel_resolution / 5 in each dimension.
        /// 
        /// Trilinear interpolation formula for normalized coordinates (tx, ty, tz) ∈ [0,1]³:
        /// D(tx,ty,tz) = D000(1-tx)(1-ty)(1-tz) + D100(tx)(1-ty)(1-tz) +
        ///               D010(1-tx)(ty)(1-tz)   + D110(tx)(ty)(1-tz)   +
        ///               D001(1-tx)(1-ty)(tz)   + D101(tx)(1-ty)(tz)   +
        ///               D011(1-tx)(ty)(tz)     + D111(tx)(ty)(tz)
        /// </summary>
        private double GetBestGammaSquared_TrilinearInterpolation(
            double Grid_BPosX, double Grid_BPosY, double Grid_BPosZ, double Grid_BDose,
            int Grid_ACenterX, int Grid_ACenterY, int Grid_ACenterZ,
            double[] Grid_ADoseArray,
            int Grid_AXSize, int Grid_AYSize, int Grid_AZSize, int Grid_AXYSize,
            double dtaSquared, double ddAbsoluteSquared)
        {
            double bestGammaSquared = double.MaxValue;
            
            // Define the search region: cubes within ±1 voxel of the Grid_A center.
            //
            // NOTE: this is narrower than the Phase 1 voxel-centre search, which spans
            // ±searchRadius voxels. On a fine grid relative to the DTA (e.g. 3 mm DTA on a
            // 1.25 mm grid, searchRadius = 3) the trilinear refinement only covers the
            // innermost cubes, so it does not refine the whole of the Phase 1 region.
            int x0 = Math.Max(0, Grid_ACenterX - 1);
            int x1 = Math.Min(Grid_AXSize - 2, Grid_ACenterX); // -2 because we need x1+1 to exist
            int y0 = Math.Max(0, Grid_ACenterY - 1);
            int y1 = Math.Min(Grid_AYSize - 2, Grid_ACenterY);
            int z0 = Math.Max(0, Grid_ACenterZ - 1);
            int z1 = Math.Min(Grid_AZSize - 2, Grid_ACenterZ);
            
            // If we don't have valid cube bounds, return
            if (x1 < x0 || y1 < y0 || z1 < z0)
                return bestGammaSquared;
            
            // Calculate voxel resolutions from position lookup tables
            double voxelResX = (Grid_AXSize > 1) ? Math.Abs(_Grid_AXPositions[1] - _Grid_AXPositions[0]) : 1.0;
            double voxelResY = (Grid_AYSize > 1) ? Math.Abs(_Grid_AYPositions[1] - _Grid_AYPositions[0]) : 1.0;
            double voxelResZ = (Grid_AZSize > 1) ? Math.Abs(_Grid_AZPositions[1] - _Grid_AZPositions[0]) : 1.0;
            
            // Step size: voxel_size / 5 in each dimension
            const int stepsPerVoxel = 5;
            
            // Iterate through all unit cubes in the search region
            for (int cubeX = x0; cubeX <= x1; cubeX++)
            {
                for (int cubeY = y0; cubeY <= y1; cubeY++)
                {
                    for (int cubeZ = z0; cubeZ <= z1; cubeZ++)
                    {
                        // Get the 8 corner doses for this unit cube
                        double D000 = Grid_ADoseArray[cubeX + cubeY * Grid_AXSize + cubeZ * Grid_AXYSize];
                        double D100 = Grid_ADoseArray[(cubeX + 1) + cubeY * Grid_AXSize + cubeZ * Grid_AXYSize];
                        double D010 = Grid_ADoseArray[cubeX + (cubeY + 1) * Grid_AXSize + cubeZ * Grid_AXYSize];
                        double D110 = Grid_ADoseArray[(cubeX + 1) + (cubeY + 1) * Grid_AXSize + cubeZ * Grid_AXYSize];
                        double D001 = Grid_ADoseArray[cubeX + cubeY * Grid_AXSize + (cubeZ + 1) * Grid_AXYSize];
                        double D101 = Grid_ADoseArray[(cubeX + 1) + cubeY * Grid_AXSize + (cubeZ + 1) * Grid_AXYSize];
                        double D011 = Grid_ADoseArray[cubeX + (cubeY + 1) * Grid_AXSize + (cubeZ + 1) * Grid_AXYSize];
                        double D111 = Grid_ADoseArray[(cubeX + 1) + (cubeY + 1) * Grid_AXSize + (cubeZ + 1) * Grid_AXYSize];
                        
                        // Physical position of cube origin corner
                        double cubeOriginX = _Grid_AXPositions[cubeX];
                        double cubeOriginY = _Grid_AYPositions[cubeY];
                        double cubeOriginZ = _Grid_AZPositions[cubeZ];
                        
                        // Sample within this unit cube
                        for (int stepIdxX = 0; stepIdxX <= stepsPerVoxel; stepIdxX++)
                        {
                            double tx = stepIdxX / (double)stepsPerVoxel;
                            double oneMinusTx = 1.0 - tx;
                            
                            double samplePosX = cubeOriginX + tx * voxelResX;
                            double distX = samplePosX - Grid_BPosX;
                            double distXSquared = distX * distX;
                            
                            if (distXSquared > dtaSquared)
                                continue;
                            
                            for (int stepIdxY = 0; stepIdxY <= stepsPerVoxel; stepIdxY++)
                            {
                                double ty = stepIdxY / (double)stepsPerVoxel;
                                double oneMinusTy = 1.0 - ty;
                                
                                double samplePosY = cubeOriginY + ty * voxelResY;
                                double distY = samplePosY - Grid_BPosY;
                                double distXYSquared = distXSquared + distY * distY;
                                
                                if (distXYSquared > dtaSquared)
                                    continue;
                                
                                // Pre-compute partial trilinear coefficients
                                double c00 = oneMinusTx * oneMinusTy;
                                double c10 = tx * oneMinusTy;
                                double c01 = oneMinusTx * ty;
                                double c11 = tx * ty;
                                
                                for (int stepIdxZ = 0; stepIdxZ <= stepsPerVoxel; stepIdxZ++)
                                {
                                    double tz = stepIdxZ / (double)stepsPerVoxel;
                                    double oneMinusTz = 1.0 - tz;
                                    
                                    double samplePosZ = cubeOriginZ + tz * voxelResZ;
                                    double distZ = samplePosZ - Grid_BPosZ;
                                    double distSquared = distXYSquared + distZ * distZ;
                                    
                                    // NOTE: this bound is 2x DTA, whereas the X and Y prunes
                                    // above use 1x DTA. The pruning is therefore asymmetric
                                    // and looser along Z than along X and Y. Any sample kept
                                    // by this looser test but lying beyond one DTA yields
                                    // γ > 1 anyway, so pass/fail is unaffected; the only
                                    // effect is some wasted work. Left as-is.
                                    if (distSquared > dtaSquared * 4.0)
                                        continue;
                                    
                                    // Trilinear interpolation
                                    double interpolatedGrid_ADose =
                                        D000 * c00 * oneMinusTz +
                                        D100 * c10 * oneMinusTz +
                                        D010 * c01 * oneMinusTz +
                                        D110 * c11 * oneMinusTz +
                                        D001 * c00 * tz +
                                        D101 * c10 * tz +
                                        D011 * c01 * tz +
                                        D111 * c11 * tz;
                                    
                                    // Calculate gamma: comparing interpolated Grid_A dose to Grid_B dose
                                    double doseDiff = interpolatedGrid_ADose - Grid_BDose;
                                    double doseDiffSquared = doseDiff * doseDiff;
                                    
                                    double gammaSquared = distSquared / dtaSquared + doseDiffSquared / ddAbsoluteSquared;
                                    
                                    if (gammaSquared < bestGammaSquared)
                                    {
                                        bestGammaSquared = gammaSquared;
                                        
                                        if (gammaSquared <= 1.0)
                                            return gammaSquared;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            return bestGammaSquared;
        }


        /// <summary>
        /// Calculate gamma squared for interpolated point along edge (analytical solution)
        /// </summary>
        private double CalculateInterpolatedGammaSquared(
            double Grid_BPosX, double Grid_BPosY, double Grid_BPosZ, double Grid_BDose,
            double anchorPosX, double anchorPosY, double anchorPosZ, double Da,
            double interpPosX, double interpPosY, double interpPosZ, double Di,
            double deltaD, double deltaX, double dtaSquared, double ddAbsoluteSquared)
        {
            // Calculate distance from Grid_B point to anchor
            double Grid_BToAnchorDx = Grid_BPosX - anchorPosX;
            double Grid_BToAnchorDy = Grid_BPosY - anchorPosY;
            double Grid_BToAnchorDz = Grid_BPosZ - anchorPosZ;
            double distGrid_BToAnchor = Math.Sqrt(Grid_BToAnchorDx * Grid_BToAnchorDx +
                                                Grid_BToAnchorDy * Grid_BToAnchorDy +
                                                Grid_BToAnchorDz * Grid_BToAnchorDz);

            double K1 = ddAbsoluteSquared / dtaSquared;

            double numerator = (Grid_BDose - Da) + K1 * distGrid_BToAnchor * deltaX / deltaD;
            double denominator = K1 * deltaX / deltaD + deltaD / deltaX;

            if (Math.Abs(denominator) < 1e-10)
                return double.MaxValue;

            double X = numerator / denominator;

            // Check if X is within valid range [0, deltaX]
            if (X < 0 || X > deltaX)
                return double.MaxValue;

            // Calculate interpolated position and dose
            double t = X / deltaX;
            double Dx = Da + deltaD * t;

            double posXinterp = anchorPosX + (interpPosX - anchorPosX) * t;
            double posYinterp = anchorPosY + (interpPosY - anchorPosY) * t;
            double posZinterp = anchorPosZ + (interpPosZ - anchorPosZ) * t;

            // Calculate gamma squared
            double distX = posXinterp - Grid_BPosX;
            double distY = posYinterp - Grid_BPosY;
            double distZ = posZinterp - Grid_BPosZ;
            double distSquared = distX * distX + distY * distY + distZ * distZ;

            double doseDiff = Dx - Grid_BDose;
            double gammaSquared = distSquared / dtaSquared + doseDiff * doseDiff / ddAbsoluteSquared;

            return gammaSquared;
        }

        private DoseGridProperties GetDoseGridProperties(Dose dose)
        {
            return new DoseGridProperties
            {
                XSize = dose.XSize,
                YSize = dose.YSize,
                ZSize = dose.ZSize,
                XRes = dose.XRes,
                YRes = dose.YRes,
                ZRes = dose.ZRes,
                XDirection = dose.XDirection,
                YDirection = dose.YDirection,
                ZDirection = dose.ZDirection,
                Origin = dose.Origin
            };
        }

        /// <summary>
        /// Writes the gamma map into the database as a new plan carrying an evaluation dose,
        /// so it can be viewed with the normal dose display tools.
        /// 
        /// This is the ONLY method that modifies the patient, and it calls
        /// Patient.BeginModifications() itself.
        /// 
        /// Known quirks, retained deliberately because clinical users are accustomed to the
        /// current behaviour. Read these before interpreting a gamma plot:
        /// 
        ///  - Gamma values are stored as a DoseValue in "%" into a dose matrix copied from
        ///    the Grid_B plan. That matrix's integer voxel scaling was chosen to span the
        ///    plan's dose range, not the 0-2 range of a gamma index, so the displayed gamma
        ///    map may be coarsely quantised.
        ///  - Sub-threshold voxels (sentinel -1) are written as 0, which is indistinguishable
        ///    from a perfect match. Low-dose regions therefore appear to pass perfectly.
        ///  - Voxels where no candidate was found hold ~1.34e154 (see CalculateGammaIndex)
        ///    and will saturate the display.
        ///  - The plan Id is truncated to 13 characters, so different parameter combinations
        ///    can collide, and re-running with the same parameters will throw on a duplicate
        ///    Id. The exception is caught and reported below.
        ///  - Course.AddExternalPlanSetup always creates a new primary reference point, so
        ///    repeated runs accumulate plans and reference points in the course.
        ///  - CopyEvaluationDose is a licensed API call.
        /// 
        /// The Grid_APlan and PassRate parameters are accepted but not currently used.
        /// </summary>
        private void CreateGammaPlan(PlanSetup Grid_APlan, PlanSetup Grid_BPlan, double[,,] gammaArray,
            Dose Grid_BDose, bool isGlobal, double dta, double dd, double minDosePercent, double PassRate)
        {
            try
            {
                // This is the only method in the script that writes to the database
                // (it adds a plan and an evaluation dose), so this is where write access
                // is requested. Kept inside the try/catch so that attempting to run on a
                // read-only or approved patient surfaces as a handled error message rather
                // than an unhandled exception.
                _context.Patient.BeginModifications();

                string glob_or_loc = isGlobal ? "Global" : "Local";
                string gammaplanId = $"{dta}mm{dd}%{minDosePercent}%min_{glob_or_loc}";
                if (gammaplanId.Length > 13)
                    gammaplanId = gammaplanId.Substring(0, 13);

                Course course = Grid_BPlan.Course;
                StructureSet structureSet = Grid_BPlan.StructureSet;

                ExternalPlanSetup gammaPlan = course.AddExternalPlanSetup(structureSet);

                gammaPlan.Id = gammaplanId;

                // Defensive no-op: AddExternalPlanSetup returns an empty plan, so there are no
                // beams to remove. Retained because CopyEvaluationDose below requires that the
                // plan contains no beams. Note this is a generic Beam, not an IonBeam — the
                // gamma plan is always an ExternalPlanSetup regardless of the modality of the
                // plans being compared.
                var beamsToRemove = gammaPlan.Beams.ToList();
                foreach (var beam in beamsToRemove)
                    gammaPlan.RemoveBeam(beam);

                // Copy Grid_B dose grid structure for gamma map
                EvaluationDose gammaDose = gammaPlan.CopyEvaluationDose(Grid_BPlan.Dose);

                for (int z = 0; z < Grid_BDose.ZSize; z++)
                {
                    var gammaPlane = new int[Grid_BDose.XSize, Grid_BDose.YSize];

                    for (int x = 0; x < Grid_BDose.XSize; x++)
                    {
                        for (int y = 0; y < Grid_BDose.YSize; y++)
                        {
                            // The -1 sentinel means "not evaluated" (Grid_B dose was below the
                            // minimum dose threshold). It is written as 0, so on the plot these
                            // voxels are indistinguishable from a perfect γ = 0 match.
                            double gammaValue = gammaArray[x, y, z];
                            DoseValue doseVal = gammaValue == -1
                                ? new DoseValue(0, "%")
                                : new DoseValue(gammaValue, "%");
                            gammaPlane[x, y] = gammaDose.DoseValueToVoxel(doseVal);
                        }
                    }

                    gammaDose.SetVoxels(z, gammaPlane);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating gamma plan: {ex.Message}\n\nStack trace: {ex.StackTrace}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class DoseGridProperties
    {
        public int XSize { get; set; }
        public int YSize { get; set; }
        public int ZSize { get; set; }
        public double XRes { get; set; }
        public double YRes { get; set; }
        public double ZRes { get; set; }
        public VVector XDirection { get; set; }
        public VVector YDirection { get; set; }
        public VVector ZDirection { get; set; }
        public VVector Origin { get; set; }
    }

    public class VoxelPosition
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
    }

    public class GammaResult
    {
        public string ResultText { get; set; }
        public double[,,] GammaArray { get; set; }
        public Dose Grid_BDose { get; set; }
        public double PassRate { get; set; }
        public double Grid_BMaxDose { get; set; }

        /// <summary>
        /// Number of Grid_B voxels actually compared: above the minimum dose threshold AND
        /// inside the extent of the Grid_A dose grid. This is the pass-rate denominator.
        /// </summary>
        public int EvaluatedVoxels { get; set; }

        /// <summary>
        /// Number of Grid_B voxels above the minimum dose threshold that were excluded
        /// because they fall outside the extent of the Grid_A dose grid. Not currently shown
        /// in the results table; available here if it is ever worth surfacing.
        /// </summary>
        public int UncoveredVoxels { get; set; }
    }
}