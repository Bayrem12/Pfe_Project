// NLP Feature Models - Match backend DTOs exactly

// ============ ENUMS ============

export enum StepIntentType {
  Navigate = 0,
  Click = 1,
  Input = 2,
  Select = 3,
  Assert = 4,
  Wait = 5,
  Custom = 6
}

export enum UIActionType {
  Click = 0,
  Type = 1,
  Select = 2,
  Navigate = 3,
  Assert = 4,
  Hover = 5,
  Scroll = 6,
  Wait = 7
}

export enum SelectorStrategy {
  Css = 'css',
  XPath = 'xpath',
  Id = 'id',
  Name = 'name',
  ClassName = 'className'
}

// ============ GHERKIN PARSER ============

export interface ParseGherkinRequest {
  gherkinContent: string;
}

export interface ParsedStep {
  keyword: string;  // Given, When, Then, And, But
  text: string;     // The step text after the keyword
}

export interface ParseGherkinResponse {
  isValid: boolean;
  steps: ParsedStep[];
  errorMessage?: string;
}

// ============ STEP ANALYSIS ============

export interface StepParameter {
  name: string;
  value: string;
  parameterType: string;
}

export interface StepAnalysis {
  id: string;
  stepId: string;
  intent: string;        // Navigate, Click, Input, etc.
  action: string;        // click, type, navigate
  target: string;        // UI element selector/description
  value?: string;        // Value to input (optional)
  confidence: number;    // 0.0 to 1.0
  parameters: StepParameter[];
}

export interface AnalyzeScenarioResponse {
  scenarioId: string;
  analyses: StepAnalysis[];
}

// ============ ACTION MAPPINGS ============

export interface CreateActionMappingRequest {
  intentPattern: string;      // Regex pattern e.g., "click.*login"
  actionType: string;         // Click, Type, Navigate, etc.
  selectorStrategy: string;   // css, xpath, id
  selectorValue: string;      // #loginBtn, //button[@id='login']
  description: string;        // Human-readable description
  priority: number;           // Higher = evaluated first (0 = default)
}

export interface ActionMapping {
  id: string;
  intentPattern: string;
  actionType: string;
  selectorStrategy: string;
  selectorValue: string;
  description: string;
  isActive: boolean;
  priority: number;
  projectId?: string;
  createdDate?: Date;
}

// ============ UI HELPER TYPES ============

export interface ActionTypeOption {
  value: string;
  label: string;
  icon: string;
}

export const ACTION_TYPE_OPTIONS: ActionTypeOption[] = [
  { value: 'Click', label: 'Click', icon: 'mouse' },
  { value: 'Type', label: 'Type/Input', icon: 'keyboard' },
  { value: 'Select', label: 'Select', icon: 'list' },
  { value: 'Navigate', label: 'Navigate', icon: 'open_in_browser' },
  { value: 'Assert', label: 'Assert/Verify', icon: 'check_circle' },
  { value: 'Hover', label: 'Hover', icon: 'pan_tool' },
  { value: 'Scroll', label: 'Scroll', icon: 'swap_vert' },
  { value: 'Wait', label: 'Wait', icon: 'hourglass_empty' }
];

export interface SelectorStrategyOption {
  value: string;
  label: string;
  example: string;
}

export const SELECTOR_STRATEGY_OPTIONS: SelectorStrategyOption[] = [
  { value: 'css', label: 'CSS Selector', example: '#loginBtn, .submit-button' },
  { value: 'xpath', label: 'XPath', example: '//button[@id="login"]' },
  { value: 'id', label: 'ID', example: 'loginBtn' },
  { value: 'name', label: 'Name', example: 'username' },
  { value: 'className', label: 'Class Name', example: 'btn-primary' }
];

// Gherkin keyword styling
export interface GherkinKeyword {
  keyword: string;
  color: string;
  bgColor: string;
}

export const GHERKIN_KEYWORDS: GherkinKeyword[] = [
  { keyword: 'Given', color: '#1e40af', bgColor: '#dbeafe' },
  { keyword: 'When', color: '#7c3aed', bgColor: '#ede9fe' },
  { keyword: 'Then', color: '#059669', bgColor: '#d1fae5' },
  { keyword: 'And', color: '#d97706', bgColor: '#fef3c7' },
  { keyword: 'But', color: '#dc2626', bgColor: '#fee2e2' }
];
