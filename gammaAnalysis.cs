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


//[assembly: AssemblyVersion("1.1.0")]
//[assembly: AssemblyFileVersion("1.1.0")]
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
            window.Width = 800;
            window.Height = 800;
        }
    }

    public partial class GammaIndexWindow : UserControl
    {
        private ScriptContext _context;
        private ComboBox _referencePlanCombo;
        private ComboBox _targetPlanCombo;
        private ComboBox _calculationTypeCombo;
        private ComboBox _GammaPlotCombo;
        private TextBox _dtaTextBox;
        private TextBox _ddTextBox;
        private TextBox _minDoseTextBox;
        private TextBlock _resultsTextBlock;
        private Button _calculateButton;

        // Pre-computed lookup tables for performance
        private double[] _targetXPositions;
        private double[] _targetYPositions;
        private double[] _targetZPositions;

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

            var refPlanLabel = new Label { Content = "Reference Plan:", FontWeight = FontWeights.Bold };
            Grid.SetRow(refPlanLabel, 0);
            mainGrid.Children.Add(refPlanLabel);

            _referencePlanCombo = new ComboBox { Margin = new Thickness(0, 5, 0, 10) };
            Grid.SetRow(_referencePlanCombo, 1);
            mainGrid.Children.Add(_referencePlanCombo);

            var targetPlanLabel = new Label { Content = "Target Plan:", FontWeight = FontWeights.Bold };
            Grid.SetRow(targetPlanLabel, 2);
            mainGrid.Children.Add(targetPlanLabel);

            _targetPlanCombo = new ComboBox { Margin = new Thickness(0, 5, 0, 10) };
            Grid.SetRow(_targetPlanCombo, 3);
            mainGrid.Children.Add(_targetPlanCombo);

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
                FontFamily = new FontFamily("Consolas")  // Important for table alignment!
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
                _referencePlanCombo.Items.Add(plan);
                _targetPlanCombo.Items.Add(plan);
            }

            if (plansWithDose.Count > 0)
            {
                _referencePlanCombo.SelectedIndex = 0;
                if (plansWithDose.Count > 1)
                    _targetPlanCombo.SelectedIndex = 1;
            }
        }

        private void CalculateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _calculateButton.IsEnabled = false;
                Mouse.OverrideCursor = Cursors.Wait;
                
                // Initialize headers only if this is the first calculation
                if (string.IsNullOrEmpty(_resultsTextBlock.Text))
                {
                    _resultsTextBlock.Text = "Gamma Index Analysis Results\n";
                    _resultsTextBlock.Text += CreateTableHeader();
                }
                
                var referencePlan = _referencePlanCombo.SelectedItem as PlanSetup;
                var targetPlan = _targetPlanCombo.SelectedItem as PlanSetup;
                
                if (referencePlan == null || targetPlan == null)
                {
                    MessageBox.Show("Please select both reference and target plans.");
                    return;
                }
                if (referencePlan == targetPlan)
                {
                    MessageBox.Show("Reference and target plans must be different.");
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
                var result = CalculateGammaIndexOptimized(referencePlan, targetPlan, dta, dd, minDosePercent, isGlobal);
                
                // Append the formatted result row
                _resultsTextBlock.Text += CreateTableRow(
                    referencePlan.Id, 
                    targetPlan.Id, 
                    dta, 
                    dd, 
                    isGlobal ? "Global" : "Local",
                    minDosePercent,
                    result.PassRate
                );
                
                bool createGammaPlot = _GammaPlotCombo.SelectedItem.ToString() == "Yes";
                if (createGammaPlot)
                {
                    CreateGammaPlan(referencePlan, targetPlan, result.GammaArray, result.ReferenceDose, isGlobal, dta, dd, minDosePercent, result.PassRate);
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
            // Define column widths
            int refPlanWidth = 20;
            int targetPlanWidth = 20;
            int dtaWidth = 8;
            int ddWidth = 8;
            int typeWidth = 8;
            int minDoseWidth = 10;
            int passRateWidth = 10;
            
            
            string header = 
                PadRight("Ref Plan", refPlanWidth) + " | " +
                PadRight("Target Plan", targetPlanWidth) + " | " +
                PadRight("DTA(mm)", dtaWidth) + " | " +
                PadRight("DD(%)", ddWidth) + " | " +
                PadRight("Type", typeWidth) + " | " +
                PadRight("MinDose%", minDoseWidth) + " | " +
                PadRight("Pass%", passRateWidth) + " | ";
               
            
            // Add separator line
            int totalWidth = refPlanWidth + targetPlanWidth + dtaWidth + ddWidth + typeWidth + 
                            minDoseWidth + passRateWidth + (8 * 3); // 8 separators * 3 chars
            header += new string('-', totalWidth) + "\n";
            
            return header;
        }

        private string CreateTableRow(string refPlan, string targetPlan, double dta, double dd, 
                                    string calcType, double minDosePercent, double passRate)
        {
            // Same column widths as header
            int refPlanWidth = 20;
            int targetPlanWidth = 20;
            int dtaWidth = 8;
            int ddWidth = 8;
            int typeWidth = 8;
            int minDoseWidth = 10;
            int passRateWidth = 10;
           
            
            string row = 
                PadRight(TruncateString(refPlan, refPlanWidth), refPlanWidth) + " | " +
                PadRight(TruncateString(targetPlan, targetPlanWidth), targetPlanWidth) + " | " +
                PadRight(dta.ToString("F1"), dtaWidth) + " | " +
                PadRight(dd.ToString("F1"), ddWidth) + " | " +
                PadRight(calcType, typeWidth) + " | " +
                PadRight(minDosePercent.ToString("F1"), minDoseWidth) + " | " +
                PadRight(passRate.ToString("F1"), passRateWidth) + " | ";
              
            
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
        /// Optimized gamma calculation - single-threaded with pre-computed values and flat arrays
        /// </summary>
        private GammaResult CalculateGammaIndexOptimized(PlanSetup referencePlan, PlanSetup targetPlan,
            double dta, double dd, double minDosePercent, bool isGlobal)
        {
            referencePlan.DoseValuePresentation = DoseValuePresentation.Relative;
            targetPlan.DoseValuePresentation = DoseValuePresentation.Relative;

            var referenceTotalDose = referencePlan.TotalDose;
            var targetTotalDose = targetPlan.TotalDose;
            var referenceDose = referencePlan.Dose;
            var targetDose = targetPlan.Dose;

            var refProps = GetDoseGridProperties(referenceDose);
            var targetProps = GetDoseGridProperties(targetDose);

            // Extract dose arrays as flat 1D arrays for better cache performance
            var refDoseArray = ExtractDoseArrayFlat(referenceDose, referenceTotalDose.Dose, refProps, out double refMaxDose);
            var targetDoseArray = ExtractDoseArrayFlat(targetDose, targetTotalDose.Dose, targetProps, out double targetMaxDose);

            double globalMaxDose = Math.Max(refMaxDose, targetMaxDose);
            double minDoseThreshold = (minDosePercent / 100.0) * refMaxDose;

            // Pre-compute position lookup tables for target grid
            PrecomputeTargetPositionTables(targetProps);

            // Pre-compute squared values to avoid repeated calculations
            double dtaSquared = dta * dta;
            double ddFraction = dd / 100.0;

            int refXSize = refProps.XSize;
            int refYSize = refProps.YSize;
            int refZSize = refProps.ZSize;
            int refXYSize = refXSize * refYSize;

            int targetXSize = targetProps.XSize;
            int targetYSize = targetProps.YSize;
            int targetZSize = targetProps.ZSize;
            int targetXYSize = targetXSize * targetYSize;

            // Use flat array for gamma results - better cache locality
            var gammaArrayFlat = new double[refXSize * refYSize * refZSize];

            // Initialize to -1 (not yet evaluated)
            for (int i = 0; i < gammaArrayFlat.Length; i++)
                gammaArrayFlat[i] = -1.0;

            // Calculate search radius based on DTA and voxel size
            double minTargetRes = Math.Min(targetProps.XRes, Math.Min(targetProps.YRes, targetProps.ZRes));
            int searchRadius = Math.Max(1, (int)Math.Ceiling(dta / minTargetRes));

            int totalVoxels = 0;
            int passedVoxels = 0;

            // Pre-compute reference grid directions for position calculation
            double refXDirX = refProps.XRes * refProps.XDirection.x;
            double refXDirY = refProps.XRes * refProps.XDirection.y;
            double refXDirZ = refProps.XRes * refProps.XDirection.z;
            double refYDirX = refProps.YRes * refProps.YDirection.x;
            double refYDirY = refProps.YRes * refProps.YDirection.y;
            double refYDirZ = refProps.YRes * refProps.YDirection.z;
            double refZDirX = refProps.ZRes * refProps.ZDirection.x;
            double refZDirY = refProps.ZRes * refProps.ZDirection.y;
            double refZDirZ = refProps.ZRes * refProps.ZDirection.z;
            double refOriginX = refProps.Origin.x;
            double refOriginY = refProps.Origin.y;
            double refOriginZ = refProps.Origin.z;

            // Pre-compute target grid inverse transform for position-to-voxel conversion
            double invTargetXRes = 1.0 / targetProps.XRes;
            double invTargetYRes = 1.0 / targetProps.YRes;
            double invTargetZRes = 1.0 / targetProps.ZRes;
            double targetOriginX = targetProps.Origin.x;
            double targetOriginY = targetProps.Origin.y;
            double targetOriginZ = targetProps.Origin.z;
            double targetXDirX = targetProps.XDirection.x;
            double targetXDirY = targetProps.XDirection.y;
            double targetXDirZ = targetProps.XDirection.z;
            double targetYDirX = targetProps.YDirection.x;
            double targetYDirY = targetProps.YDirection.y;
            double targetYDirZ = targetProps.YDirection.z;
            double targetZDirX = targetProps.ZDirection.x;
            double targetZDirY = targetProps.ZDirection.y;
            double targetZDirZ = targetProps.ZDirection.z;

            // Main loop - iterate through reference voxels
            // Loop order optimized for memory access pattern (z outer for slice-based data)
            for (int z = 0; z < refZSize; z++)
            {
                double zContribX = refOriginX + z * refZDirX;
                double zContribY = refOriginY + z * refZDirY;
                double zContribZ = refOriginZ + z * refZDirZ;

                for (int y = 0; y < refYSize; y++)
                {
                    double yzContribX = zContribX + y * refYDirX;
                    double yzContribY = zContribY + y * refYDirY;
                    double yzContribZ = zContribZ + y * refYDirZ;

                    for (int x = 0; x < refXSize; x++)
                    {
                        int refIdx = x + y * refXSize + z * refXYSize; // converts indexs over x, y, z to a flat index value by multiplying by array size in different dimensions
                        double refDoseValue = refDoseArray[refIdx];

                        // Skip voxels below threshold
                        if (refDoseValue < minDoseThreshold)
                            continue;

                        totalVoxels++; // we start counting here

                        // Calculate reference position (inline for performance, i.e. nested addition is faster)
                        double refPosX = yzContribX + x * refXDirX; // this is actually =  refOriginX + ( z * refZDirX + y * refYDirX + x * refXDirX  )
                        double refPosY = yzContribY + x * refXDirY;
                        double refPosZ = yzContribZ + x * refXDirZ;

                        // Convert reference position to target voxel coordinates (inline)
                        double relX = refPosX - targetOriginX;
                        double relY = refPosY - targetOriginY;
                        double relZ = refPosZ - targetOriginZ;

                        int targetCenterX = (int)Math.Round((relX * targetXDirX + relY * targetXDirY + relZ * targetXDirZ) * invTargetXRes);
                        int targetCenterY = (int)Math.Round((relX * targetYDirX + relY * targetYDirY + relZ * targetYDirZ) * invTargetYRes);
                        int targetCenterZ = (int)Math.Round((relX * targetZDirX + relY * targetZDirY + relZ * targetZDirZ) * invTargetZRes);

                        // Calculate dose difference reference
                        double ddReference = isGlobal ? globalMaxDose : refDoseValue;
                        double ddAbsolute = ddFraction * ddReference;
                        double ddAbsoluteSquared = ddAbsolute * ddAbsolute;

                        double minGammaSquared = double.MaxValue;
                        bool foundPass = false;

                        // Search in target neighborhood - voxel center check first
                        int zMin = Math.Max(0, targetCenterZ - searchRadius);
                        int zMax = Math.Min(targetZSize - 1, targetCenterZ + searchRadius);
                        int yMin = Math.Max(0, targetCenterY - searchRadius);
                        int yMax = Math.Min(targetYSize - 1, targetCenterY + searchRadius);
                        int xMin = Math.Max(0, targetCenterX - searchRadius);
                        int xMax = Math.Min(targetXSize - 1, targetCenterX + searchRadius);

                        for (int tz = zMin; tz <= zMax && !foundPass; tz++)
                        {
                            double targetPosZ = _targetZPositions[tz];
                            double distZ = targetPosZ - refPosZ;
                            double distZSquared = distZ * distZ;

                            // Early rejection if Z distance alone exceeds DTA
                            if (distZSquared > dtaSquared)
                                continue;

                            for (int ty = yMin; ty <= yMax && !foundPass; ty++)
                            {
                                double targetPosY = _targetYPositions[ty];
                                double distY = targetPosY - refPosY;
                                double distYZSquared = distY * distY + distZSquared;

                                // Early rejection if Y+Z distance exceeds DTA
                                if (distYZSquared > dtaSquared)
                                    continue;

                                for (int tx = xMin; tx <= xMax; tx++)
                                {
                                    int targetIdx = tx + ty * targetXSize + tz * targetXYSize;
                                    double targetDoseValue = targetDoseArray[targetIdx];

                                    double targetPosX = _targetXPositions[tx];
                                    double distX = targetPosX - refPosX;
                                    double distSquared = distX * distX + distYZSquared;

                                    // Calculate gamma squared (avoid sqrt until final result)
                                    double doseDiff = targetDoseValue - refDoseValue;
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

                        // If voxel center search didn't find a pass, try edge interpolation
                        if (!foundPass && minGammaSquared > 1.0)
                        {
                            double interpolatedGammaSquared = GetBestGammaSquaredOptimized(
                                refPosX, refPosY, refPosZ, refDoseValue,
                                targetCenterX, targetCenterY, targetCenterZ,
                                targetDoseArray, targetXSize, targetYSize, targetZSize, targetXYSize,
                                dtaSquared, ddAbsoluteSquared);

                            if (interpolatedGammaSquared < minGammaSquared)
                                minGammaSquared = interpolatedGammaSquared;
                        }

                        // Store final gamma value (take sqrt only once at the end)
                        gammaArrayFlat[refIdx] = Math.Sqrt(minGammaSquared);

                        if (minGammaSquared <= 1.0)
                            passedVoxels++;
                    }
                }
            }

            // Convert flat array back to 3D for compatibility with CreateGammaPlan
            var gammaArray = new double[refXSize, refYSize, refZSize];
            for (int z = 0; z < refZSize; z++)
            {
                for (int y = 0; y < refYSize; y++)
                {
                    for (int x = 0; x < refXSize; x++)
                    {
                        gammaArray[x, y, z] = gammaArrayFlat[x + y * refXSize + z * refXYSize];
                    }
                }
            }

            double passRate = totalVoxels > 0 ? (passedVoxels / (double)totalVoxels) * 100.0 : 0.0;
            
            string resultText = $"Gamma Analysis Results:\n" +
                                $"Reference Plan: {referencePlan.Id}\n" +
                                $"Target Plan: {targetPlan.Id}\n" +
                                $"DTA: {dta} mm\n" +
                                $"DD: {dd}% ({(isGlobal ? "Global" : "Local")})\n" +
                                $"Min Dose Threshold: {minDosePercent}% ({minDoseThreshold:F2} {targetTotalDose.UnitAsString})\n" +
                                $"Total Evaluated Voxels: {totalVoxels:N0}\n" +
                                $"Passed Voxels (γ ≤ 1): {passedVoxels:N0}\n" +
                                $"Pass Rate: {passRate:F1}%\n" +
                                $"Reference Max Dose: {refMaxDose:F2} {referenceTotalDose.UnitAsString}\n" +
                                $"Target Max Dose: {targetMaxDose:F2} {targetTotalDose.UnitAsString}";

            return new GammaResult
            {
                ResultText = resultText,
                GammaArray = gammaArray,
                ReferenceDose = referenceDose,
                PassRate = passRate,
            };
        }

        /// <summary>
        /// Pre-compute position lookup tables for the target grid
        /// This avoids repeated VVector calculations in the inner loop
        /// </summary>
        private void PrecomputeTargetPositionTables(DoseGridProperties props)
        {
            _targetXPositions = new double[props.XSize];
            _targetYPositions = new double[props.YSize];
            _targetZPositions = new double[props.ZSize];

            // For each axis, pre-compute the position values
            // Assumes axis-aligned grids
            for (int i = 0; i < props.XSize; i++)
                _targetXPositions[i] = props.Origin.x + i * props.XRes * props.XDirection.x;

            for (int i = 0; i < props.YSize; i++)
                _targetYPositions[i] = props.Origin.y + i * props.YRes * props.YDirection.y;

            for (int i = 0; i < props.ZSize; i++)
                _targetZPositions[i] = props.Origin.z + i * props.ZRes * props.ZDirection.z;
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
        /// Optimized interpolation search - returns gamma squared to avoid sqrt
        /// </summary>
        private double GetBestGammaSquaredOptimized(
            double refPosX, double refPosY, double refPosZ, double refDose,
            int tx, int ty, int tz,
            double[] targetDoseArray,
            int targetXSize, int targetYSize, int targetZSize, int targetXYSize,
            double dtaSquared, double ddAbsoluteSquared)
        {
            double bestGammaSquared = double.MaxValue;

            // Clamp center voxel to valid range
            tx = Math.Max(0, Math.Min(tx, targetXSize - 1));
            ty = Math.Max(0, Math.Min(ty, targetYSize - 1));
            tz = Math.Max(0, Math.Min(tz, targetZSize - 1));

            int anchorIdx = tx + ty * targetXSize + tz * targetXYSize;
            double Da = targetDoseArray[anchorIdx];

            double anchorPosX = _targetXPositions[tx];
            double anchorPosY = _targetYPositions[ty];
            double anchorPosZ = _targetZPositions[tz];

            // Check 6 face neighbors (positive directions only to avoid duplicates)
            // Direction: +X
            if (tx + 1 < targetXSize)
            {
                int ix = tx + 1;
                int interpIdx = ix + ty * targetXSize + tz * targetXYSize;
                double Di = targetDoseArray[interpIdx];
                double deltaD = Di - Da;

                if (Math.Abs(deltaD) > 1e-10)
                {
                    double interpPosX = _targetXPositions[ix];
                    double edgeDx = interpPosX - anchorPosX;
                    double deltaX = Math.Abs(edgeDx);

                    double gammaSquared = CalculateInterpolatedGammaSquared(
                        refPosX, refPosY, refPosZ, refDose,
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
            if (ty + 1 < targetYSize)
            {
                int iy = ty + 1;
                int interpIdx = tx + iy * targetXSize + tz * targetXYSize;
                double Di = targetDoseArray[interpIdx];
                double deltaD = Di - Da;

                if (Math.Abs(deltaD) > 1e-10)
                {
                    double interpPosY = _targetYPositions[iy];
                    double edgeDy = interpPosY - anchorPosY;
                    double deltaX = Math.Abs(edgeDy);

                    double gammaSquared = CalculateInterpolatedGammaSquared(
                        refPosX, refPosY, refPosZ, refDose,
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
            if (tz + 1 < targetZSize)
            {
                int iz = tz + 1;
                int interpIdx = tx + ty * targetXSize + iz * targetXYSize;
                double Di = targetDoseArray[interpIdx];
                double deltaD = Di - Da;

                if (Math.Abs(deltaD) > 1e-10)
                {
                    double interpPosZ = _targetZPositions[iz];
                    double edgeDz = interpPosZ - anchorPosZ;
                    double deltaX = Math.Abs(edgeDz);

                    double gammaSquared = CalculateInterpolatedGammaSquared(
                        refPosX, refPosY, refPosZ, refDose,
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
            if (tx - 1 >= 0)
            {
                int ix = tx - 1;
                int interpIdx = ix + ty * targetXSize + tz * targetXYSize;
                double Di = targetDoseArray[interpIdx];
                double deltaD = Di - Da;

                if (Math.Abs(deltaD) > 1e-10)
                {
                    double interpPosX = _targetXPositions[ix];
                    double edgeDx = interpPosX - anchorPosX;
                    double deltaX = Math.Abs(edgeDx);

                    double gammaSquared = CalculateInterpolatedGammaSquared(
                        refPosX, refPosY, refPosZ, refDose,
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
            if (ty - 1 >= 0)
            {
                int iy = ty - 1;
                int interpIdx = tx + iy * targetXSize + tz * targetXYSize;
                double Di = targetDoseArray[interpIdx];
                double deltaD = Di - Da;

                if (Math.Abs(deltaD) > 1e-10)
                {
                    double interpPosY = _targetYPositions[iy];
                    double edgeDy = interpPosY - anchorPosY;
                    double deltaX = Math.Abs(edgeDy);

                    double gammaSquared = CalculateInterpolatedGammaSquared(
                        refPosX, refPosY, refPosZ, refDose,
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
            if (tz - 1 >= 0)
            {
                int iz = tz - 1;
                int interpIdx = tx + ty * targetXSize + iz * targetXYSize;
                double Di = targetDoseArray[interpIdx];
                double deltaD = Di - Da;

                if (Math.Abs(deltaD) > 1e-10)
                {
                    double interpPosZ = _targetZPositions[iz];
                    double edgeDz = interpPosZ - anchorPosZ;
                    double deltaX = Math.Abs(edgeDz);

                    double gammaSquared = CalculateInterpolatedGammaSquared(
                        refPosX, refPosY, refPosZ, refDose,
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
        /// Calculate gamma squared for interpolated point along edge
        /// </summary>
        private double CalculateInterpolatedGammaSquared(
            double refPosX, double refPosY, double refPosZ, double refDose,
            double anchorPosX, double anchorPosY, double anchorPosZ, double Da,
            double interpPosX, double interpPosY, double interpPosZ, double Di,
            double deltaD, double deltaX, double dtaSquared, double ddAbsoluteSquared)
        {
            // Calculate distance from reference to anchor
            double refToAnchorDx = refPosX - anchorPosX;
            double refToAnchorDy = refPosY - anchorPosY;
            double refToAnchorDz = refPosZ - anchorPosZ;
            double distRefToAnchor = Math.Sqrt(refToAnchorDx * refToAnchorDx +
                                               refToAnchorDy * refToAnchorDy +
                                               refToAnchorDz * refToAnchorDz);

            double K1 = ddAbsoluteSquared / dtaSquared;

            double numerator = (refDose - Da) + K1 * distRefToAnchor * deltaX / deltaD;
            double denominator = K1 * deltaX / deltaD + deltaD / deltaX;

            if (Math.Abs(denominator) < 1e-10)
                return double.MaxValue;

            double X = numerator / denominator;

            // Check if X is within valid range [0, deltaX]
            if (X < 0 || X > deltaX)
                return double.MaxValue;

            // Calculate interpolated position and dose linearly
            double t = X / deltaX;
            double Dx = Da + deltaD * t;

            double posXinterp = anchorPosX + (interpPosX - anchorPosX) * t;
            double posYinterp = anchorPosY + (interpPosY - anchorPosY) * t;
            double posZinterp = anchorPosZ + (interpPosZ - anchorPosZ) * t;

            // Calculate gamma squared
            double distX = posXinterp - refPosX;
            double distY = posYinterp - refPosY;
            double distZ = posZinterp - refPosZ;
            double distSquared = distX * distX + distY * distY + distZ * distZ;

            double doseDiff = Dx - refDose;
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

        private void CreateGammaPlan(PlanSetup referencePlan, PlanSetup targetPlan, double[,,] gammaArray,
            Dose referenceDose, bool isGlobal, double dta, double dd, double minDosePercent, double PassRate)
        {
            try
            {
                string glob_or_loc = isGlobal ? "Global" : "Local";
                string gammaplanId = $"{dta}mm{dd}%{minDosePercent}%min_{glob_or_loc}";
                if (gammaplanId.Length > 13)
                    gammaplanId = gammaplanId.Substring(0, 13);

                Course course = referencePlan.Course;
                StructureSet structureSet = referencePlan.StructureSet;

                ExternalPlanSetup gammaPlan = course.AddExternalPlanSetup(structureSet);

                gammaPlan.Id = gammaplanId;

                var beamsToRemove = gammaPlan.Beams.ToList();
                foreach (var beam in beamsToRemove) // remove beams so we can copy the evaluation dose
                    gammaPlan.RemoveBeam(beam);
                
                // Store base plan and timestamp in plan comment box
                //string datetime = DateTime.Now.ToString("HH:mm:ss, yyy-MM-dd");
                //gammaPlan.Comment = ("Reference Plan: " + referencePlan.Id + ". Target Plan: " + targetPlan.Id + "at" + datetime + "\n DTA: "+dta+" mm. DD: "+dd+" %. "+"Max Dose Cutoff: "+minDosePercent+" %. "+glob_or_loc);
                

                EvaluationDose evaluationDose = gammaPlan.CopyEvaluationDose(referencePlan.Dose);

                for (int z = 0; z < referenceDose.ZSize; z++)
                {
                    var gammaPlane = new int[referenceDose.XSize, referenceDose.YSize];

                    for (int x = 0; x < referenceDose.XSize; x++)
                    {
                        for (int y = 0; y < referenceDose.YSize; y++)
                        {
                            double gammaValue = gammaArray[x, y, z];
                            DoseValue doseVal = gammaValue == -1
                                ? new DoseValue(0, "%")
                                : new DoseValue(gammaValue, "%");
                            gammaPlane[x, y] = evaluationDose.DoseValueToVoxel(doseVal);
                        }
                    }

                    evaluationDose.SetVoxels(z, gammaPlane);
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
        public Dose ReferenceDose { get; set; }
        public double PassRate { get; set; }
    }
}