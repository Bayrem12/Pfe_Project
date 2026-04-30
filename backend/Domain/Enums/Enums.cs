namespace Domain.Enums
{
        public enum ScenarioStatus
        {

            Draft = 0,

            Active = 1,

            Archived = 2

        }

        public enum StepType
        {

            Given = 0,

            When = 1,

            Then = 2,

            And = 3,

            But = 4

        }

        public enum StepIntentType
        {

            Navigate = 0,

            Click = 1,

            Input = 2,

            Select = 3,

            Assert = 4,

            Wait = 5,

            Custom = 6

        }

        public enum ExecutionStatus
        {

            Pending = 0,

            Running = 1,

            Completed = 2,

            Failed = 3,

            Cancelled = 4

        }

        public enum TestStatus
        {

            Passed = 0,

            Failed = 1,

            Skipped = 2,

            Error = 3

        }

        public enum StepStatus
        {

            Passed = 0,

            Failed = 1,

            Skipped = 2,

            Error = 3

        }

        public enum UIActionType
        {

            Click = 0,

            Type = 1,

            Select = 2,

            Navigate = 3,

            Assert = 4,

            Hover = 5,

            Scroll = 6,

            Wait = 7

        }

        public enum DetectionMethod
        {

            Selector = 0,

            XPath = 1,

            CSS = 2,

            OCR = 3,

            YOLO = 4,

            OpenCV = 5

        }

        public enum ProjectRole
        {

            Owner = 0,

            Manager = 1,

            Tester = 2,

            Viewer = 3

        }

        public enum ReportFormat
        {

            PDF = 0,

            HTML = 1

        }

        public enum LogLevel
        {
            Debug = 0,

            Info = 1,

            Warning = 2,

            Error = 3,

            Critical = 4
        }

    }


