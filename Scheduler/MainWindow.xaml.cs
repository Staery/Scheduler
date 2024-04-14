using Scheduler.Components;
using Scheduler.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static System.Net.Mime.MediaTypeNames;
using System.Linq;
using System.Xml.Linq;

namespace Scheduler
{
    public partial class MainWindow : Window
    {
        private ObjectPool<Grid> _gridPool = new ObjectPool<Grid>();
        private ObjectPool<Rectangle> _rectanglePool = new ObjectPool<Rectangle>();
        private ObjectPool<TextBlock> _textBlocPool = new ObjectPool<TextBlock>();
        //List active elements
        private List<FrameworkElement> _elements = new List<FrameworkElement>();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();
        private Stopwatch _stopwatch = new Stopwatch();
        private const int TimeLineRowHeight = 30; // Constant for defining the height of each timeline row
        private readonly MainViewModel _viewModel; // ViewModel instance
        private double _windowWidth { get; set; } // Window width
        private double _windowHeight { get; set; } // Window height

        DispatcherTimer timer = null;

        // Constructor
        public MainWindow()
        {
            InitializeComponent();

            AllocConsole();

            _viewModel = new MainViewModel(new CustomDateFormatter()); // Initializing ViewModel
            DataContext = _viewModel;
            _viewModel.DataHandled += ViewModel_DataHandled;
            _viewModel.WrongInputDetected += ViewModel_WrongInputDetected;

            // Event handlers
            btnGenerateSchedule.Click += BtnGenerateSchedule_Click;
            scrollViewerOuter.ScrollChanged += ScrollViewerOuter_ScrollChanged;
            this.Loaded += SchedulerWindow_Loaded;
            this.SizeChanged += SchedulerWindow_SizeChanged;

            // Generating random events
            _viewModel.CreateRandomEvents();
        }

        // Event handler for wrong input detected
        private void ViewModel_WrongInputDetected(object sender, MessageEventArgs e)
        {
            MessageBox.Show(e.Message, e.Caption); // Show error message in a message box
        }

        // Event handler for data handled
        private void ViewModel_DataHandled(object sender, EventArgs e)
        {
            mainGrid.Width = _viewModel.TimeLineEnd * 30; // Setting width of main grid based on timeline end

            SetupTimeLines(); // Setting up timeline
            if (timer == null)
            {
                timer = new DispatcherTimer();
                timer.Tick += Timer_Tick;
                timer.Interval = new TimeSpan(0, 0, 0, 0, 10);
                timer.Start();
            }
    
            rectCurrentTime.Margin = new Thickness(); // Resetting margin of current time rectangle
        }

        // Event handler for scroll change in outer scroll viewer
        private void ScrollViewerOuter_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            _stopwatch.Restart();
            if (!_viewModel.IsGenerating)
            {
                DrawVisibleEvents(); // Draw visible events on scroll change
            }
            _stopwatch.Stop();

            TimeSpan deltaTime = _stopwatch.Elapsed;

            double fps = 1.0 / deltaTime.TotalSeconds;
            Console.WriteLine($"FPS: {fps} {deltaTime.TotalMilliseconds}");
        }

        // Event handler for clicking generate schedule button
        private void BtnGenerateSchedule_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.IsValidParams())
            {
                scrollViewerOuter.ScrollToLeftEnd(); // Scroll to left end
                ResetGraphic(); // Reset graphic elements
                Task.Run(() => { _viewModel.CreateRandomEvents(); }); // Create random events asynchronously
            }
        }

        // Method to draw visible events
        private void DrawVisibleEvents()
        {

            ClearUnusedEvents();  // Clear or hide events that are not visible

            DrawRuler();
            double containerWidth = mainGrid.ActualWidth;

            var events = _viewModel.GetVisibleEvents(containerWidth, scrollViewerOuter.HorizontalOffset, scrollViewerOuter.HorizontalOffset + _windowWidth);

            foreach (var timeLineEvent in events)
            {
                DrawEvent(timeLineEvent); // Reuse or create new visual representations for events
            }

            void DrawEvent(ScheduleEvent timeLineEvent)
            {
                Grid eventGrid = _gridPool.Get();
                Rectangle rectangle = _rectanglePool.Get();
                TextBlock textBlock = _textBlocPool.Get();

                _elements.Add(eventGrid);
                _elements.Add(rectangle);
                _elements.Add(textBlock);

                ConfigureEventGrid(eventGrid, timeLineEvent, containerWidth);
                ConfigureRectangle(rectangle, timeLineEvent);
                ConfigureTextBlock(textBlock, timeLineEvent);


                if (!gridTimeLines.Children.Contains(eventGrid))
                {
                    gridTimeLines.Children.Add(eventGrid);
                }

                eventGrid.Children.Add(rectangle);

                eventGrid.Children.Add(textBlock);
            }
        }

        private void ConfigureEventGrid(Grid grid, ScheduleEvent timeLineEvent, double width)
        {
            grid.HorizontalAlignment = HorizontalAlignment.Left;
            grid.Width = timeLineEvent.DurationRatio * width;
            grid.Margin = new Thickness(timeLineEvent.StartTimeRatio * width, 2, 0, 2);
            Grid.SetRow(grid, timeLineEvent.RenderLayer);
            Panel.SetZIndex(grid, 10);
        }

        private void ConfigureRectangle(Rectangle rectangle, ScheduleEvent timeLineEvent)
        {
            rectangle.StrokeThickness = 3;
            rectangle.Stroke = Brushes.Black;
            rectangle.Opacity = 0.8;
            rectangle.Fill = GetBrushByStatus(timeLineEvent.Status);
        }

        private void ConfigureTextBlock(TextBlock textBlock, ScheduleEvent timeLineEvent)
        {
            textBlock.Text = "Generic Name";
            textBlock.VerticalAlignment = VerticalAlignment.Center;
            textBlock.Margin = new Thickness(5, 0, 0, 0);
        }

        private Brush GetBrushByStatus(int status)
        {
            // Replace with actual status-to-color logic
            switch (status)
            {
                case 1: return Brushes.Orange;
                case 2: return Brushes.Red;
                case 3: return Brushes.LightGreen;
                default: return Brushes.Gray;
            }
        }

        private void ClearUnusedEvents()
        {
            void DisconectFromParent(FrameworkElement frameworkElement)
            {
                if (frameworkElement.Parent is Panel panel)
                {
                    panel.Children.Remove(frameworkElement);
                }
            }
            foreach (var element in _elements)
            {
                DisconectFromParent(element);
                if (element is Grid grid)
                {
                    _gridPool.Release(grid);
                }

                else if (element is TextBlock textBlock)
                {
                    _textBlocPool.Release(textBlock);
                }
                else if (element is Rectangle rectangle)
                {
                    _rectanglePool.Release(rectangle);
                }
                else { throw new InvalidOperationException(); }
            }

            _elements.Clear();
        }



        // Method to reset graphic elements
        private void ResetGraphic()
        {
            gridTimeStamps.Children.Clear();
            gridTimeStampsMicroSteps.Children.Clear();
            gridTimeLines.Children.Clear();
            gridTimeLines.RowDefinitions.Clear();
        }

        // Method to draw ruler
        private void DrawRuler()
        {
            int duration = _viewModel.TimeLineEnd;

            double timelineDuration = duration;
            double containerWidth = gridTimeStamps.ActualWidth;

            for (int i = 0; i < duration; i += _viewModel.RulerStep)
            {
                double timeStampMargin = ((double)i) / timelineDuration * containerWidth;
                double timeStampWidth = ((double)MainViewModel.RulerStepValue / timelineDuration) * containerWidth;

                if (timeStampMargin >= scrollViewerOuter.HorizontalOffset - _windowWidth / MainViewModel.RulerStepValue &&
                    timeStampMargin <= scrollViewerOuter.HorizontalOffset + _windowWidth)
                {
                    Grid grid = new Grid
                    {
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(timeStampMargin, 0, 0, 0),
                        Width = timeStampWidth * 2
                    };

                    TextBlock textBlock = new TextBlock
                    {
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        Text = i.ToString(),
                        Margin = new Thickness(5, 0, 0, 0)
                    };

                    grid.Children.Add(textBlock);

                    Style borderStyle = FindResource("blackBorder") as Style;

                    Border border = new Border
                    {
                        Style = borderStyle
                    };

                    grid.Children.Add(border);
                    gridTimeStamps.Children.Add(grid);

                    for (int j = 1; j <= MainViewModel.RulerStepValue; j++)
                    {
                        double timeStampMicroMargin = ((double)(i + j)) / timelineDuration * containerWidth;

                        Border bor = new Border
                        {
                            Margin = new Thickness(timeStampMicroMargin, 0, 0, 0),
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Style = borderStyle
                        };

                        gridTimeStampsMicroSteps.Children.Add(bor);
                        Grid.SetRow(bor, 3);
                    }
                }
            }
        }

        // Method to set up timeline
        private void SetupTimeLines()
        {
            for (int i = 0; i < _viewModel.GridRowDefinitionsCount; i++)
            {
                gridTimeLines.RowDefinitions.Add(new RowDefinition() { MinHeight = TimeLineRowHeight, MaxHeight = TimeLineRowHeight });

                Border border = new Border();
                if (i % 2 == 0)
                {
                    border.Background = Brushes.AliceBlue;
                }
                else
                {
                    border.Background = Brushes.LightGray;
                }
                gridTimeLines.Children.Add(border);
                Grid.SetRow(border, i);
            }
        }

        // Method to set scroll view offset
        private void SetScrollViewOffset()
        {
            _windowWidth = this.RenderSize.Width;
            _windowHeight = this.RenderSize.Height;
        }

        // Timer tick event handler
        private void Timer_Tick(object sender, EventArgs e)
        {
            rectCurrentTime.Margin = new Thickness(rectCurrentTime.Margin.Left + 0.4, rectCurrentTime.Margin.Top, rectCurrentTime.Margin.Right, rectCurrentTime.Margin.Bottom);
        }

        // Event handler for window size change
        private void SchedulerWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SetScrollViewOffset();
        }

        // Event handler for window loaded
        private void SchedulerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetScrollViewOffset();
        }
    }
}
