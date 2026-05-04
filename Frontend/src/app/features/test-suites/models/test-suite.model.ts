// Test Suite Feature Models - Match backend DTOs exactly

// ============ TEST SUITE DTOs ============

export interface TestSuiteDto {
  id: string;
  projectId: string;
  name: string;
  description: string;
  createdById: string;
  createdDate: string | null;
  modifiedDate: string | null;
  scenarioCount: number;
}

export interface TestSuiteScenarioDto {
  id: string;
  scenarioId: string;
  scenarioTitle: string;
  scenarioDescription: string;
  displayOrder: number;
}

export interface TestSuiteWithCasesDto {
  id: string;
  projectId: string;
  name: string;
  description: string;
  createdById: string;
  createdDate: string | null;
  modifiedDate: string | null;
  scenarios: TestSuiteScenarioDto[];
}

// ============ REQUEST TYPES ============

export interface CreateTestSuiteRequest {
  projectId: string;
  name: string;
  description: string;
  createdById: string;
}

export interface UpdateTestSuiteRequest {
  id: string;
  name: string;
  description: string;
}

// ============ SCENARIO DTO (for available scenarios list) ============

export enum ScenarioStatus {
  Draft = 0,
  Active = 1,
  Archived = 2
}

export interface ScenarioDto {
  id: string;
  featureId: string;
  featureName: string;
  title: string;
  description: string;
  status: ScenarioStatus;
  currentVersion: number;
  createdAt: string;
  updatedAt: string | null;
  stepCount: number;
}
