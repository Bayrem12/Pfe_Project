export interface TestRunListItem {
  id: string;
  runLabel: string;
  status: string;
  startedAt: string;
  completedAt: string | null;
  durationSeconds: number | null;
  executedBy: string;
  environment: string;
  projectId: string | null;
  projectName: string;
  scenarioName: string | null;
  moduleName: string | null;
  featureName: string | null;
  totalTests: number;
  passedTests: number;
  failedTests: number;
  skippedTests: number;
  passRate: number;
}

export interface TestRunListPayload {
  items: TestRunListItem[];
  total: number;
  page: number;
  pageSize: number;
}

export interface TestRunScreenshot {
  id: string;
  fileName: string;
  filePath: string;
  capturedAt: string;
}

export interface TestRunStepResult {
  id: string;
  stepText: string;
  stepType: string;
  status: string;
  errorMessage: string | null;
  durationSeconds: number;
  actionPerformed: string;
  selectorUsed: string;
  screenshot: TestRunScreenshot | null;
}

export interface TestRunScenarioResult {
  id: string;
  scenarioId: string;
  scenarioName: string;
  status: string;
  errorMessage: string | null;
  startedAt: string;
  completedAt: string;
  durationSeconds: number;
  stepResults: TestRunStepResult[];
}

export interface TestRunLog {
  id: string;
  level: string;
  message: string;
  details: string | null;
  timestamp: string;
}

export interface TestRunDetail {
  id: string;
  runLabel: string;
  status: string;
  startedAt: string;
  completedAt: string | null;
  durationSeconds: number | null;
  browserType: string;
  isHeadless: boolean;
  executedBy: string;
  environment: string;
  projectId: string | null;
  projectName: string;
  scenarioName: string | null;
  testSuiteName: string | null;
  totalTests: number;
  passedTests: number;
  failedTests: number;
  skippedTests: number;
  passRate: number;
  testResults: TestRunScenarioResult[];
  logs: TestRunLog[];
  reportUrl?: string | null;
  reports?: TestRunReport[];
}

export interface TestRunReport {
  id: string;
  format: string;
  url: string;
}
