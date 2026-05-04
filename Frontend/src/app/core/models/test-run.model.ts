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

export interface AiFailureAnalysis {
  category: string;
  root_cause: string;
  title: string;
  explanation: string;
  where: string;
  is_test_issue: boolean;
  suggested_fix: string;
  confidence: number;
  scenario_name?: string;
  failed_step_count?: number;
  first_failed_step?: string;
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
  aiAnalysis?: AiFailureAnalysis | null;
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
  aiAnalysis?: AiFailureAnalysis | null;
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
