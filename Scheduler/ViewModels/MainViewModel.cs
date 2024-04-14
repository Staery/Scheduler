using System;
using PropertyChanged; // Library for implementing INotifyPropertyChanged interface
using System.Windows.Threading;
using System.Windows;
using Scheduler.Components;
using Bitlush; 
using System.Collections.Generic;

namespace Scheduler.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    public class MainViewModel
    {
        #region consts
        // Constants for defining minimum and maximum event duration, amount of events, and layers
        private const int MinEventDuration = 1;
        private const int MaxEventDuration = 15;
        private const int MaxEventsAmount = 1000000;
        private const int MinEventsAmount = 15;
        private const int MaxLayersAmount = 100;
        public const int RulerStepValue = 5;
        #endregion

        // Properties for data binding
        public string Date { get; set; }
        public int EventsAmount { get; set; } = 3000;
        public int LayersAmount { get; set; } = 20;
        public int PendingAmount { get; set; }
        public int JeopardyAmount { get; set; }
        public int CompletedAmount { get; set; }
        public bool IsGenerating { get; set; }
        public int TimeLineEnd { get; set; }
        public int RulerStep => TimeLineEnd / (TimeLineEnd / RulerStepValue);
        public int GridRowDefinitionsCount { get; set; }

        public AvlTree<double, ScheduleEvent> MainTree { get; set; }

        // Constructor injecting a DateFormatter
        public MainViewModel(IDateFormatter dateFormatter)
        {
            Date = dateFormatter.GenerateCustomDate(); // Generate custom date using injected formatter
        }

        // Method to check if provided parameters are valid
        public bool IsValidParams()
        {
            string message = null;
            // Check if the provided event and layer amounts are within allowed ranges
            if (EventsAmount > MaxEventsAmount || EventsAmount < MinEventsAmount || LayersAmount > MaxLayersAmount)
            {
                message = $"Invalid values:\nLayers = {LayersAmount} (Allowed [1 - {MaxLayersAmount}])\n" +
                    $"Events = {EventsAmount} (Allowed [{MinEventsAmount} - {MaxEventsAmount}])";
            }
            if (message == null)
            {
                return true;
            }
            else
            {
                // Invoke event to notify about wrong input
                WrongInputDetected?.Invoke(this, new MessageEventArgs() { Message = message, Caption = "Wrong input" });
                return false;
            }
        }

        // Method to create random events
        public void CreateRandomEvents()
        {
            IsGenerating = true;
            GridRowDefinitionsCount = 0;
            TimeLineEnd = EventsAmount - (EventsAmount % RulerStepValue);

            int minEventEntryAt = 0;
            int maxEventEntryAt = TimeLineEnd - MaxEventDuration;

            PendingAmount = 0;
            JeopardyAmount = 0;
            CompletedAmount = 0;

            int eventsPerLayer = EventsAmount / LayersAmount;
            MainTree = new AvlTree<double, ScheduleEvent>();
            Random random = new Random();

            // Generate events for each layer
            for (int i = 0; i < LayersAmount; i++)
            {
                for (int j = 0; j <= eventsPerLayer; j++)
                {
                    // Generate random event parameters
                    int duration = random.Next(MinEventDuration, MaxEventDuration + 1);
                    int start = random.Next(minEventEntryAt, maxEventEntryAt + 1);
                    int type = random.Next(1, 3 + 1);
                    switch (type)
                    {
                        case 1:
                            PendingAmount++;
                            break;
                        case 2:
                            JeopardyAmount++;
                            break;
                        case 3:
                            CompletedAmount++;
                            break;
                    }
                    // Calculate event margins and width ratios
                    double eventMarginMultiplayer = (double)start / (double)TimeLineEnd;
                    double eventWidthMultiplayer = (double)duration / (double)TimeLineEnd;

                    // Create new ScheduleEvent and insert it into the AVL tree
                    ScheduleEvent mEvent = new ScheduleEvent()
                    {
                        Duration = duration,
                        StartTime = start,
                        Status = type,
                        StartTimeRatio = eventMarginMultiplayer,
                        DurationRatio = eventWidthMultiplayer,
                        RenderLayer = i
                    };
                    MainTree.Insert(eventMarginMultiplayer, mEvent);
                }
                GridRowDefinitionsCount++;
            }
            IsGenerating = false;

            // Dispatch event to handle data
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                DataHandled?.Invoke(this, EventArgs.Empty);
            }));
        }

        // Method to get visible events within specified boundaries
        public List<ScheduleEvent> GetVisibleEvents(double containerWidth, double leftBoundary, double rigthBoundary)
        {
            List<ScheduleEvent> events = new List<ScheduleEvent>();
            AvlNode<double, ScheduleEvent> rootNode = MainTree.Root;
            if (rootNode != null)
            {
                HandleNode(rootNode);
            }
            void HandleNode(AvlNode<double, ScheduleEvent> node)
            {
                // Traverse AVL tree and add visible events to the list
                if (node.Key * containerWidth + node.Value.DurationRatio * containerWidth < leftBoundary)
                {
                    if (node.Right != null)
                    {
                        HandleNode(node.Right);
                    }
                }
                else if (node.Key * containerWidth > rigthBoundary)
                {
                    if (node.Left != null)
                    {
                        HandleNode(node.Left);
                    }
                }
                else
                {
                    ScheduleEvent timeLineEvent = node.Value;
                    if (timeLineEvent.IsRendered == false)
                    {
                        events.Add(timeLineEvent);
                    }
                    if (node.Left != null)
                    {
                        HandleNode(node.Left);
                    }
                    if (node.Right != null)
                    {
                        HandleNode(node.Right);
                    }
                }
            }
            return events;
        }

        // Event for handling data
        public event EventHandler DataHandled;
        // Delegate for event handling wrong input
        public delegate void MessageEventHandler(object sender, MessageEventArgs e);
        // Event for wrong input detection
        public event MessageEventHandler WrongInputDetected;
    }
}
