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
            context.Patient.BeginModifications();
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

        // Pre-computed lookup tables for performance
        // These are for the Grid_A grid (the one we search within)
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

            foreach (var plan in course.ExternalPlanSetups)
            {
                if (plan.Dose != null)
                    plansWithDose.Add(plan);
            }

            foreach (var plan in course.IonPlanSetups)
            {
                if (plan.Dose != null)
                    plansWithDose.Add(plan);
            }

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
        /// Clinical terminology:
        /// - Grid_A = Ground truth dose distribution (we SEARCH within this)
        /// - Grid_B = Dose being checked (we ITERATE through this)
        /// 
        /// For each voxel in the Grid_B dose grid, we search the Grid_A dose grid
        /// to find the point that minimises the gamma index.
        /// 
        /// Gamma formula:
        /// γ = min{ sqrt[ (r_Grid_A - r_Grid_B)² / DTA² + (D_Grid_A - D_Grid_B)² / DD² ] }
        /// 
        /// A voxel passes if γ ≤ 1.0
        /// </summary>
        private GammaResult CalculateGammaIndex(PlanSetup Grid_APlan, PlanSetup Grid_BPlan,
            double dta, double dd, double minDosePercent, bool isGlobal)
        {
            // Set dose presentation to relative for both plans
            Grid_APlan.DoseValuePresentation = DoseValuePresentation.Relative;
            Grid_BPlan.DoseValuePresentation = DoseValuePresentation.Relative;

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

            // For global gamma, normalise to Grid_B max dose (the ground truth)
            double globalMaxDose = Grid_BMaxDose;

            // Minimum dose threshold is relative to Grid_B max dose
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

            // Initialize to -1 (not yet Grid_B)
            for (int i = 0; i < gammaArrayFlat.Length; i++)
                gammaArrayFlat[i] = -1.0;

            // Calculate search radius based on DTA and Grid_A voxel size
            double minGrid_ARes = Math.Min(Grid_AProps.XRes, Math.Min(Grid_AProps.YRes, Grid_AProps.ZRes));
            int searchRadius = Math.Max(1, (int)Math.Ceiling(dta / minGrid_ARes));

            int totalVoxels = 0;
            int passedVoxels = 0;

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

                        // Skip voxels below threshold (threshold based on Grid_A max)
                        if (Grid_BDoseValue < minDoseThreshold)
                            continue;

                        totalVoxels++;

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

                        // Calculate dose difference criterion
                        // For global: normalise to Grid_A max dose
                        // For local: normalise to Grid_B dose at this point
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

                        // Store final gamma value
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
            
            string resultText = $"Gamma Analysis Results:\n" +
                                $"Grid_A Plan (Ground Truth): {Grid_APlan.Id}\n" +
                                $"Grid_B Plan (Being Checked): {Grid_BPlan.Id}\n" +
                                $"DTA: {dta} mm\n" +
                                $"DD: {dd}% ({(isGlobal ? "Global" : "Local")})\n" +
                                $"Min Dose Threshold: {minDosePercent}% ({minDoseThreshold:F2} {Grid_ATotalDose.UnitAsString})\n" +
                                $"Total Grid_B Voxels: {totalVoxels:N0}\n" +
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
                Grid_BMaxDose = Grid_BMaxDose
            };
        }

        /// <summary>
        /// Pre-compute position lookup tables for the Grid_A grid
        /// (the grid we search within to find best gamma match)
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
        /// Extract dose array as flat 1D array for better cache performance
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

            // Clamp center voxel to valid range
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
            
            // Define the search region: cubes within ±1 voxel of the Grid_A center
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

        private void CreateGammaPlan(PlanSetup Grid_APlan, PlanSetup Grid_BPlan, double[,,] gammaArray,
            Dose Grid_BDose, bool isGlobal, double dta, double dd, double minDosePercent, double PassRate)
        {
            try
            {
                string glob_or_loc = isGlobal ? "Global" : "Local";
                string gammaplanId = $"{dta}mm{dd}%{minDosePercent}%min_{glob_or_loc}";
                if (gammaplanId.Length > 13)
                    gammaplanId = gammaplanId.Substring(0, 13);

                Course course = Grid_BPlan.Course;
                StructureSet structureSet = Grid_BPlan.StructureSet;

                ExternalPlanSetup gammaPlan = course.AddExternalPlanSetup(structureSet);

                gammaPlan.Id = gammaplanId;

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
    }
}