import { Injectable, inject } from '@angular/core';
import { Observable, map, forkJoin, of, catchError } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/services/auth.service';
import {
  DashboardData,
  DashboardSummary,
  DashboardSummaryDto,
  ExecutionTrend,
  ExecutionTrendsDto,
  ProjectStatisticsDto,
  TestDistribution,
  RecentActivity,
  AIAlert,
  GlobalStatus,
  PagedAuditLogsResponse
} from '../models/dashboard.model';

@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  private authService = inject(AuthService);

  constructor(private apiService: ApiService) { }

  /**
   * Get all active projects for project selector
   */
  getActiveProjects(): Observable<{ id: string; name: string }[]> {
    const userId = this.authService.getCurrentUserId();
    if (!userId) {
      return of([]);
    }
    
    return this.apiService.get<any>('projet', { 
      userId: userId,
      pageNumber: 1, 
      pageSize: 100 
    }).pipe(
      map(response => {
        const projects = response.resultat.items || [];
        return projects
          .filter((p: any) => p.isActive && !p.isDeleted)
          .map((p: any) => ({
            id: p.id,
            name: p.name
          }));
      }),
      catchError(error => {
        console.error('Error fetching projects:', error);
        return of([]);
      })
    );
  }

  /**
   * Get complete dashboard data from real backend API
   * Combines global summary, execution trends, and audit logs
   */
  getDashboardData(projectId?: string): Observable<DashboardData> {
    return forkJoin({
      summary: this.getDashboardSummary(),
      projects: this.getActiveProjects(),
      auditLogs: this.getAuditLogs(1, 5)
    }).pipe(
      map(({ summary, projects, auditLogs }) => {
        // Use first active project if no projectId provided
        const selectedProjectId = projectId || (projects.length > 0 ? projects[0].id : null);
        
        const data: DashboardData = {
          user: {
            name: this.getUserName(),
            greeting: this.getGreeting()
          },
          summary: this.transformSummary(summary),
          executionTrends: [], // Will be loaded separately
          distribution: this.getDistributionFromSummary(summary),
          recentActivities: this.transformAuditLogsToActivities(auditLogs.items),
          aiAlerts: this.generateAIAlerts(summary),
          globalStatus: this.getGlobalStatus(),
          lastUpdated: new Date(),
          availableProjects: projects,
          selectedProjectId: selectedProjectId
        };
        return data;
      })
    );
  }

  /**
   * Get execution trends for a specific project
   * Separate method to allow loading trends independently
   */
  loadExecutionTrends(projectId: string, days: number = 14): Observable<ExecutionTrend[]> {
    return this.getExecutionTrends(projectId, days).pipe(
      map(dto => this.transformTrends(dto))
    );
  }

  /**
   * Get global dashboard summary from backend
   * API: GET /api/dashboard/summary
   */
  getDashboardSummary(): Observable<DashboardSummaryDto> {
    return this.apiService.get<DashboardSummaryDto>('dashboard/summary').pipe(
      map(response => response.resultat)
    );
  }

  /**
   * Get paginated audit logs
   * API: GET /api/dashboard/audit-logs?page=1&pageSize=50
   */
  getAuditLogs(page: number = 1, pageSize: number = 50): Observable<PagedAuditLogsResponse> {
    return this.apiService.get<PagedAuditLogsResponse>(`dashboard/audit-logs`, { page, pageSize }).pipe(
      map(response => response.resultat)
    );
  }

  /**
   * Get execution trends for a specific project
   * API: GET /api/dashboard/projects/{projectId}/trends?days=14
   */
  getExecutionTrends(projectId: string, days: number = 14): Observable<ExecutionTrendsDto> {
    return this.apiService.get<ExecutionTrendsDto>(`dashboard/projects/${projectId}/trends`, { days }).pipe(
      map(response => response.resultat)
    );
  }

  // Helper methods for data transformation

  private getUserName(): string {
    const user = this.authService.getCurrentUser();
    if (user) {
      // Try to get full name from user object
      if (user.fullName) return user.fullName;
      if (user.firstName && user.lastName) return `${user.firstName} ${user.lastName}`;
      if (user.firstName) return user.firstName;
      if (user.email) return user.email.split('@')[0]; // Use email username as fallback
    }
    return 'User'; // Final fallback
  }

  private getGreeting(): string {
    const hour = new Date().getHours();
    if (hour < 12) return 'Good morning';
    if (hour < 17) return 'Good afternoon'; // Changed from 18 to 17
    return 'Good evening';
  }

  private transformSummary(dto: DashboardSummaryDto): DashboardSummary {
    const passRate = dto.overallPassRate;
    const grade = this.calculateQualityGrade(passRate);
    
    return {
      // Core Statistics
      totalProjects: {
        value: dto.totalProjects,
        active: dto.activeProjects,
        change: 2, // TODO: Calculate from historical data
        trend: 'up'
      },
      totalScenarios: {
        value: dto.totalScenarios,
        change: 12, // TODO: Calculate from historical data
        trend: 'up'
      },
      totalExecutions: {
        value: dto.totalExecutions,
        pending: dto.pendingExecutions,
        change: dto.totalExecutions > 0 ? 15 : 0,
        trend: dto.totalExecutions > 0 ? 'up' : 'neutral'
      },
      passRate: {
        percentage: Math.round(passRate),
        status: passRate >= 95 ? 'improving' : passRate >= 85 ? 'stable' : 'declining'
      },
      qualityScore: {
        grade: grade,
        label: this.getGradeLabel(grade),
        metricsCount: 48 // TODO: Get from backend if available
      },
      testCoverage: {
        percentage: this.calculateCoverage(dto),
        goal: 90
      }
    };
  }

  private calculateQualityGrade(passRate: number): 'A' | 'B' | 'C' | 'D' | 'F' {
    if (passRate >= 95) return 'A';
    if (passRate >= 85) return 'B';
    if (passRate >= 75) return 'C';
    if (passRate >= 65) return 'D';
    return 'F';
  }

  private getGradeLabel(grade: string): string {
    const labels: { [key: string]: string } = {
      'A': 'Excellent',
      'B': 'Good',
      'C': 'Average',
      'D': 'Below Average',
      'F': 'Needs Improvement'
    };
    return labels[grade] || 'Unknown';
  }

  private calculateCoverage(dto: DashboardSummaryDto): number {
    // Calculate coverage based on scenarios vs total possible
    // For now, use execution ratio as a proxy
    if (dto.totalScenarios === 0) return 0;
    const ratio = dto.totalExecutions / (dto.totalScenarios * 10); // Assuming 10 expected runs per scenario
    return Math.min(Math.round(ratio * 100), 100);
  }

  /**
   * Transform real API trends data to chart format
   */
  private transformTrends(dto: ExecutionTrendsDto): ExecutionTrend[] {
    return dto.dataPoints.map(point => {
      const date = new Date(point.date);
      const dayName = date.toLocaleDateString('en-US', { weekday: 'short' });
      const dayNum = date.getDate();
      
      return {
        date: `${dayName} ${dayNum}`,
        totalExecutions: point.totalExecutions,
        passed: point.passed,
        failed: point.failed,
        passRate: point.passRate
      };
    });
  }

  private getDistributionFromSummary(dto: DashboardSummaryDto): TestDistribution {
    const passed = Math.round(dto.totalExecutions * (dto.overallPassRate / 100));
    const failed = dto.totalExecutions - passed;
    
    return {
      passed,
      failed,
      skipped: dto.pendingExecutions,
      total: dto.totalExecutions + dto.pendingExecutions
    };
  }

  private transformAuditLogsToActivities(auditLogs: any[]): RecentActivity[] {
    return auditLogs.slice(0, 5).map(log => ({
      id: log.id,
      type: this.getActivityType(log.action),
      title: this.formatAuditTitle(log),
      description: this.formatAuditDescription(log),
      timestamp: new Date(log.timestamp),
      user: log.userId
    }));
  }

  private getActivityType(action: string): 'success' | 'warning' | 'error' | 'info' {
    switch (action.toLowerCase()) {
      case 'created': return 'success';
      case 'updated': return 'info';
      case 'deleted': return 'warning';
      case 'failed': return 'error';
      default: return 'info';
    }
  }

  private formatAuditTitle(log: any): string {
    return `${log.action} ${log.entityType}`;
  }

  private formatAuditDescription(log: any): string {
    const timeAgo = this.getTimeAgo(new Date(log.timestamp));
    return `${timeAgo} • ${log.ipAddress}`;
  }

  private getTimeAgo(date: Date): string {
    const seconds = Math.floor((new Date().getTime() - date.getTime()) / 1000);
    
    if (seconds < 60) return `${seconds} seconds ago`;
    if (seconds < 3600) return `${Math.floor(seconds / 60)} mins ago`;
    if (seconds < 86400) return `${Math.floor(seconds / 3600)} hours ago`;
    return `${Math.floor(seconds / 86400)} days ago`;
  }

  private generateAIAlerts(dto: DashboardSummaryDto): AIAlert[] {
    const alerts: AIAlert[] = [];

    // Generate alerts based on actual data
    if (dto.overallPassRate < 85) {
      alerts.push({
        id: '1',
        severity: 'critical',
        title: 'Critical',
        message: `Pass rate is below 85% (currently ${dto.overallPassRate.toFixed(1)}%). Immediate attention required.`,
        actionLabel: 'View Failed Tests',
        actionType: 'view-failures',
        icon: 'warning'
      });
    }

    if (dto.pendingExecutions > 10) {
      alerts.push({
        id: '2',
        severity: 'warning',
        title: 'Pending Executions',
        message: `${dto.pendingExecutions} test executions are pending. Consider scaling resources.`,
        actionLabel: 'Manage Queue',
        actionType: 'manage-queue',
        icon: 'schedule'
      });
    }

    // Add optimization suggestion
    alerts.push({
      id: '3',
      severity: 'info',
      title: 'Optimization Suggestion',
      message: `With ${dto.totalScenarios} scenarios and ${dto.activeProjects} active projects, optimizing test suites could improve efficiency.`,
      actionLabel: 'Learn More',
      actionType: 'optimize',
      icon: 'auto_awesome'
    });

    return alerts;
  }

  /**
   * Get project statistics for comparison chart.
   * Calls the per-project statistics endpoint for each active project.
   */
  getProjectComparisonData(projects: { id: string; name: string }[]): Observable<ProjectStatisticsDto[]> {
    if (!projects || projects.length === 0) return of([]);
    
    const requests = projects.slice(0, 5).map(p =>
      this.apiService.get<ProjectStatisticsDto>(`dashboard/projects/${p.id}/statistics`).pipe(
        map(response => response.resultat),
        catchError(() => of(null))
      )
    );

    return forkJoin(requests).pipe(
      map(results => results.filter((r): r is ProjectStatisticsDto => r !== null))
    );
  }

  private getGlobalStatus(): GlobalStatus {
    // TODO: Add actual system metrics API endpoint
    return {
      latency: 24,
      uptime: 99.98,
      regions: [
        {
          name: 'Primary Server',
          status: 'active',
          latency: 18,
          position: { x: 33, y: 25 }
        },
        {
          name: 'Backup Server',
          status: 'active',
          latency: 24,
          position: { x: 50, y: 30 }
        }
      ]
    };
  }
}

