export enum ScenarioStatus {
  Draft = 'Draft',
  Active = 'Active',
  Archived = 'Archived',
  Deprecated = 'Deprecated'
}

export enum StepType {
  Given = 'Given',
  When = 'When',
  Then = 'Then',
  And = 'And',
  But = 'But'
}

export interface ScenarioDto {
  id: string;
  featureId: string;
  featureName: string;
  moduleName?: string;
  title: string;
  description: string;
  status: ScenarioStatus;
  currentVersion: number;
  createdAt: Date;
  updatedAt?: Date;
  stepCount: number;
  tags?: string[];
  lastTestStatus?: string;
  qualityScore?: number;
  qualityLabel?: 'good' | 'medium' | 'poor';
  lastAnalyzedAt?: Date;
}

export interface ScenarioDetailDto {
  id: string;
  featureId: string;
  featureName: string;
  moduleId?: string;
  moduleName?: string;
  projectId?: string;
  projectName?: string;
  title: string;
  description: string;
  gherkinContent: string;
  status: ScenarioStatus;
  currentVersion: number;
  createdAt: Date;
  updatedAt?: Date;
  steps: StepDto[];
  versions: ScenarioVersionDto[];
  tags?: string[];
  lastTestStatus?: string;
  qualityScore?: number;
  qualityLabel?: 'good' | 'medium' | 'poor';
  lastAnalyzedAt?: Date;
}

export interface StepDto {
  id: string;
  stepType: StepType;
  text: string;
  displayOrder: number;
}

export interface ScenarioVersionDto {
  id: string;
  versionNumber: number;
  gherkinContent: string;
  changeDescription: string;
  createdAt: Date;
}

export interface CreateScenarioRequest {
  featureId: string;
  title: string;
  description: string;
  gherkinContent: string;
  status?: string;
  tags?: string[];
}

export interface UpdateScenarioRequest {
  title: string;
  description: string;
  gherkinContent: string;
  changeDescription?: string;
  status?: string;
  tags?: string[];
}

export interface ValidationResultDto {
  isValid: boolean;
  errors: string[];
  warnings: string[];
}

export interface ImportScenarioRequest {
  featureId: string;
  featureFileContent: string;
}

