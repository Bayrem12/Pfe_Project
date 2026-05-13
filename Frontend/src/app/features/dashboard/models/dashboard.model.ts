// Dashboard DTOs matching backend response structure

// Global Dashboard Summary (GET /api/dashboard/summary)
export interface DashboardSummaryDto {
  totalProjects: number;
  totalScenarios: number;
  totalExecutions: number;
  overallPassRate: number;
  activeProjects: number;
  pendingExecutions: number;
}

// Project Statistics (GET /api/dashboard/projects/{projectId}/statistics)
export interface ProjectStatisticsDto {
  projectId: string;
  projectName: string;
  totalScenarios: number;
  totalExecutions: number;
  passedTests: number;
  failedTests: number;
  passRate: number;
  averageExecutionTime: number;
  lastExecutionDate: Date | null;
}

// Execution Trends (GET /api/dashboard/projects/{projectId}/trends?days=7)
export interface ExecutionTrendsDto {
  projectId: string;
  days: number;
  dataPoints: TrendDataPointDto[];
}

export interface TrendDataPointDto {
  date: Date;
  totalExecutions: number;
  passed: number;
  failed: number;
  passRate: number;
}

// Audit Logs (GET /api/dashboard/audit-logs?page=1&pageSize=50)
export interface AuditLogDto {
  id: string;
  userId: string;
  action: string;
  entityType: string;
  entityId: string | null;
  oldValues: string | null;
  newValues: string | null;
  timestamp: Date;
  ipAddress: string;
}

export interface PagedAuditLogsResponse {
  items: AuditLogDto[];
  currentPage: number;
  totalPages: number;
  pageSize: number;
  totalCount: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

// UI-friendly models (transformed from DTOs)
export interface DashboardSummary {
  // Project Statistics
  totalProjects: ProjectStatCard;

  // Test Statistics
  totalScenarios: StatCard;
  totalExecutions: ExecutionStatCard;

  // Execution Results (computed from real backend data)
  passedRuns: PassedRunsCard;
  failedRuns: FailedRunsCard;
  pendingRuns: PendingRunsCard;
}

export interface ProjectStatCard {
  value: number;
  active: number;
  change: number;
  trend: 'up' | 'down' | 'stable' | 'neutral';
}

export interface StatCard {
  value: number;
  change: number;
  trend: 'up' | 'down' | 'stable' | 'neutral';
}

export interface ExecutionStatCard {
  value: number;
  pending: number;
  change: number;
  trend: 'up' | 'down' | 'stable' | 'neutral';
}

export interface PassedRunsCard {
  count: number;
  percentage: number;
  trend: 'up' | 'down' | 'stable' | 'neutral';
}

export interface FailedRunsCard {
  count: number;
  percentage: number;
  hasFailures: boolean;
}

export interface PendingRunsCard {
  count: number;
  isUrgent: boolean;
}

// Test Execution Trends (matches real API data)
export interface ExecutionTrend {
  date: string;
  totalExecutions: number;
  passed: number;
  failed: number;
  passRate: number;
}

// Distribution Chart
export interface TestDistribution {
  passed: number;
  failed: number;
  skipped?: number;
  total: number;
}

// Recent Activity
export interface RecentActivity {
  id: string;
  type: 'success' | 'warning' | 'error' | 'info';
  title: string;
  description: string;
  timestamp: Date;
  user?: string;
}

// AI Alerts
export interface AIAlert {
  id: string;
  severity: 'critical' | 'warning' | 'info';
  title: string;
  message: string;
  actionLabel: string;
  actionType: string;
  icon?: string;
}

// Global Status
export interface GlobalStatus {
  latency: number;
  uptime: number;
  regions: ServerRegion[];
}

export interface ServerRegion {
  name: string;
  status: 'active' | 'inactive' | 'warning';
  latency: number;
  position: { x: number; y: number };
}

// Complete Dashboard Data
export interface DashboardData {
  user: {
    name: string;
    greeting: string;
  };
  summary: DashboardSummary;
  executionTrends: ExecutionTrend[];
  distribution: TestDistribution;
  recentActivities: RecentActivity[];
  aiAlerts: AIAlert[];
  globalStatus: GlobalStatus;
  lastUpdated: Date;
  availableProjects?: { id: string; name: string }[];
  selectedProjectId?: string | null;
}

