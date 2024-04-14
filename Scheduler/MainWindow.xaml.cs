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

namespace Scheduler
{
    public partial class MainWindow : Window
    {
        private const int TimeLineRowHeight = 30; // Constant for defining the height of each timeline row
        private readonly MainViewModel _viewModel; // ViewModel instance
        private double _windowWidth { get; set; } // Window width
        private double _windowHeight { get; set; } // Window height

        // Constructor
        public MainWindow()
        {
            InitializeComponent();

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

            DispatcherTimer timer = new DispatcherTimer();
            timer.Tick += Timer_Tick;
            timer.Interval = new TimeSpan(0, 0, 0, 0, 10);
            timer.Start();

            rectCurrentTime.Margin = new Thickness(); // Resetting margin of current time rectangle
        }

        // Event handler for scroll change in outer scroll viewer
        private void ScrollViewerOuter_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (!_viewModel.IsGenerating)
            {
                DrawVisibleEvents(); // Draw visible events on scroll change
            }
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
            DrawRuler(); // Draw ruler
            double containerWidth = mainGrid.ActualWidth;
            List<ScheduleEvent> events = _viewModel.GetVisibleEvents(containerWidth, scrollViewerOuter.HorizontalOffset,
                scrollViewerOuter.HorizontalOffset + _windowWidth);

            foreach (var timeLineEvent in events)
            {
                DrawEvent(timeLineEvent); // Draw each visible event
            }

            void DrawEvent(ScheduleEvent timeLineEvent)
            {
                // Draw event rectangle
                Grid eventGrid = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = timeLineEvent.DurationRatio * containerWidth,
                    Margin = new Thickness(timeLineEvent.StartTimeRatio * containerWidth, 2, 0, 2)
                };
                gridTimeLines.Children.Add(eventGrid);

                Rectangle rectangle = new Rectangle
                {
                    StrokeThickness = 3,
                    Stroke = Brushes.Black,
                    Opacity = 0.8
                };
                switch (timeLineEvent.Status)
                {
                    case 1:
                        rectangle.Fill = Brushes.Orange;
                        break;
                    case 2:
                        rectangle.Fill = Brushes.Red;
                        break;
                    case 3:
                        rectangle.Fill = Brushes.LightGreen;
                        break;
                    default:
                        rectangle.Fill = Brushes.Gray;
                        break;
                }
                eventGrid.Children.Add(rectangle);

                TextBlock textBlock = new TextBlock
                {
                    Text = "Generic Name",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5, 0, 0, 0)
                };

                eventGrid.Children.Add(textBlock);
                Panel.SetZIndex(eventGrid, 10);
                Grid.SetRow(eventGrid, timeLineEvent.RenderLayer);
            }
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
