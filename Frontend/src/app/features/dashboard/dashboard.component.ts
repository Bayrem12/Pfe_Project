import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject, interval } from 'rxjs';
import { switchMap, takeUntil } from 'rxjs/operators';
import { DashboardService } from './services/dashboard.service';
import { DashboardData } from './models/dashboard.model';
import { EnhancedStatCardsComponent } from './components/enhanced-stat-cards/enhanced-stat-cards.component';
import { ExecutionTrendsChartComponent } from './components/execution-trends-chart/execution-trends-chart.component';
import { TestDistributionChartComponent } from './components/test-distribution-chart/test-distribution-chart.component';
import { RecentActivityComponent } from './components/recent-activity/recent-activity.component';
import { AiAlertsPanelComponent } from './components/ai-alerts-panel/ai-alerts-panel.component';
import { PassRateTrendChartComponent } from './components/pass-rate-trend-chart/pass-rate-trend-chart.component';
import { ProjectComparisonChartComponent, ProjectComparisonData } from './components/project-comparison-chart/project-comparison-chart.component';
import { WeeklyActivityChartComponent } from './components/weekly-activity-chart/weekly-activity-chart.component';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    EnhancedStatCardsComponent,
    ExecutionTrendsChartComponent,
    TestDistributionChartComponent,
    RecentActivityComponent,
    AiAlertsPanelComponent,
    PassRateTrendChartComponent,
    ProjectComparisonChartComponent,
    WeeklyActivityChartComponent
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit, OnDestroy {
  private dashboardService = inject(DashboardService);
  private destroy$ = new Subject<void>();
  private stopPolling$ = new Subject<void>();
  
  dashboardData: DashboardData | null = null;
  isLoading = true;
  isRefreshing = false;
  isLiveMode = true;
  currentDate = '';
  lastUpdated = new Date();
  refreshInterval = 30000;
  
  // Project selection
  selectedProjectId: string | null = null;
  isLoadingTrends = false;
  
  // Project comparison data
  projectComparisonData: ProjectComparisonData[] = [];

  ngOnInit(): void {
    this.setCurrentDate();
    this.loadDashboardData();
    this.startAutoRefresh();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private startAutoRefresh(): void {
    if (!this.isLiveMode) return;
    
    this.stopPolling$.next();
    interval(this.refreshInterval).pipe(
      takeUntil(this.stopPolling$),
      takeUntil(this.destroy$),
      switchMap(() => this.dashboardService.getDashboardData(this.selectedProjectId || undefined))
    ).subscribe({
      next: (data) => {
        // Preserve existing trends so chart doesn't flash empty
        const existingTrends = this.dashboardData?.executionTrends || [];
        data.executionTrends = existingTrends;
        this.dashboardData = data;
        this.lastUpdated = new Date();
        // Load trends if project selected
        if (this.selectedProjectId) {
          this.loadTrendsForProject(this.selectedProjectId);
        }
      },
      error: (error) => console.error('Auto-refresh error:', error)
    });
  }

  private stopAutoRefresh(): void {
    this.stopPolling$.next();
  }

  toggleLiveMode(): void {
    this.isLiveMode = !this.isLiveMode;
    if (this.isLiveMode) {
      this.startAutoRefresh();
    } else {
      this.stopAutoRefresh();
    }
  }

  onManualRefresh(): void {
    this.isRefreshing = true;
    const existingTrends = this.dashboardData?.executionTrends || [];
    this.dashboardService.getDashboardData(this.selectedProjectId || undefined).subscribe({
      next: (data) => {
        // Preserve existing trends so the chart doesn't flash empty
        data.executionTrends = existingTrends;
        this.dashboardData = data;
        this.lastUpdated = new Date();
        this.isRefreshing = false;
        // Reload trends for the selected project
        if (this.selectedProjectId) {
          this.loadTrendsForProject(this.selectedProjectId);
        }
      },
      error: (error) => {
        console.error('Error refreshing data:', error);
        this.isRefreshing = false;
      }
    });
  }

  onRefreshIntervalChange(event: Event): void {
    const target = event.target as HTMLSelectElement;
    this.refreshInterval = parseInt(target.value);
    if (this.isLiveMode) {
      this.startAutoRefresh();
    }
  }

  loadDashboardData(): void {
    this.isLoading = true;
    this.dashboardService.getDashboardData().subscribe({
      next: (data) => {
        this.dashboardData = data;
        this.lastUpdated = new Date();
        this.isLoading = false;
        
        // Auto-select first project and load its trends
        if (data.availableProjects && data.availableProjects.length > 0) {
          this.selectedProjectId = data.availableProjects[0].id;
          this.loadTrendsForProject(this.selectedProjectId);
          this.loadProjectComparison(data.availableProjects);
        }
      },
      error: (error) => {
        console.error('Error loading dashboard data:', error);
        this.isLoading = false;
      }
    });
  }

  onProjectChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    this.selectedProjectId = select.value;
    this.loadTrendsForProject(this.selectedProjectId);
  }

  refreshTrends(): void {
    if (this.selectedProjectId) {
      this.loadTrendsForProject(this.selectedProjectId);
    }
  }

  private loadTrendsForProject(projectId: string): void {
    this.isLoadingTrends = true;
    this.dashboardService.loadExecutionTrends(projectId, 14).subscribe({
      next: (trends) => {
        if (this.dashboardData) {
          this.dashboardData.executionTrends = trends;
          this.dashboardData.selectedProjectId = projectId;
        }
        this.isLoadingTrends = false;
      },
      error: (error) => {
        console.error('Error loading trends:', error);
        this.isLoadingTrends = false;
      }
    });
  }

  private loadProjectComparison(projects: { id: string; name: string }[]): void {
    this.dashboardService.getProjectComparisonData(projects).subscribe({
      next: (stats) => {
        this.projectComparisonData = stats.map(s => ({
          projectName: s.projectName,
          passRate: s.passRate,
          scenarioCount: s.totalScenarios,
          executionCount: s.totalExecutions,
          avgExecutionTime: s.averageExecutionTime
        }));
      },
      error: (error) => console.error('Error loading project comparison:', error)
    });
  }

  private setCurrentDate(): void {
    const options: Intl.DateTimeFormatOptions = { 
      weekday: 'long', 
      year: 'numeric', 
      month: 'long', 
      day: 'numeric' 
    };
    this.currentDate = new Date().toLocaleDateString('en-US', options);
  }

  onAlertAction(event: { alert: any }): void {
    console.log('Alert action triggered:', event.alert);
  }

  getSelectedProjectName(): string {
    if (!this.dashboardData || !this.selectedProjectId) return 'All Projects';
    const project = this.dashboardData.availableProjects?.find(p => p.id === this.selectedProjectId);
    return project?.name || 'Unknown Project';
  }
}
