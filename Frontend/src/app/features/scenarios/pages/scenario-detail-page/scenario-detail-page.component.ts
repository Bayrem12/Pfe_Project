import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, ActivatedRoute } from '@angular/router';
import { ScenarioService } from '../../../../core/services/scenario.service';
import { ScenarioDetailDto, StepType } from '../../../../core/models/scenario.model';
import { BreadcrumbService } from '../../../../core/services/breadcrumb.service';
import { AuthService } from '../../../../core/services/auth.service';
import { IaAgentService } from '../../../../core/services/ia-agent.service';
import { RunNotificationsService } from '../../../../core/services/run-notifications.service';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';

@Component({
  selector: 'app-scenario-detail-page',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './scenario-detail-page.component.html'
})
export class ScenarioDetailPageComponent implements OnInit, OnDestroy {
  scenario: ScenarioDetailDto | null = null;
  scenarioId: string = '';
  loading: boolean = false;
  error: string = '';
  selectedVersion: number | null = null;
  activeTab: 'gherkin' | 'history' | 'ai' = 'gherkin';

  // AI run state
  aiRunning: boolean = false;
  aiError: string = '';
  lastAiExecutionId: string = '';

  private destroy$ = new Subject<void>();

  get canRunScenarioTests(): boolean {
    return this.authService.canRunTests();
  }

  get isViewerRole(): boolean {
    return this.authService.isProjectReadOnly();
  }

  constructor(
    private scenarioService: ScenarioService,
    private authService: AuthService,
    private breadcrumbService: BreadcrumbService,
    private iaAgentService: IaAgentService,
    private runNotifications: RunNotificationsService,
    private router: Router,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    this.route.fragment.pipe(takeUntil(this.destroy$)).subscribe(fragment => {
      if (fragment === 'ai') this.activeTab = 'ai';
    });
    this.route.params.pipe(takeUntil(this.destroy$)).subscribe(params => {
      this.scenarioId = params['id'];
      if (this.scenarioId) {
        this.loadScenario();
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.breadcrumbService.clear();
  }

  loadScenario(): void {
    this.loading = true;
    this.error = '';

    this.scenarioService.getScenarioById(this.scenarioId).subscribe({
      next: (response) => {
        if (response.status === 200 && response.resultat) {
          this.scenario = response.resultat;
          this.selectedVersion = this.scenario.currentVersion;
          this.updateBreadcrumb();
        }
        this.loading = false;
      },
      error: (err) => {
        this.error = err.error?.fail_Messages || 'Error loading scenario';
        this.loading = false;
      }
    });
  }

  private updateBreadcrumb(): void {
    if (!this.scenario) return;
    const qp = this.scenario.projectId
      ? { projectId: this.scenario.projectId, projectName: this.scenario.projectName || '' }
      : undefined;
    const crumbs: { label: string; url?: string; queryParams?: { [key: string]: string } }[] = [];
    if (this.scenario.projectName) {
      crumbs.push({ label: this.scenario.projectName, url: '/scenarios', queryParams: qp });
    }
    crumbs.push({ label: 'Scenarios', url: '/scenarios', queryParams: qp });
    crumbs.push({ label: this.scenario.title });
    this.breadcrumbService.setMultiple(crumbs);
  }

  navigateToEdit(): void {
    if (this.isViewerRole) {
      this.error = 'Viewer role is read-only and cannot edit scenarios.';
      return;
    }

    this.router.navigate(['/scenarios', this.scenarioId, 'edit']);
  }

  deleteScenario(): void {
    if (this.isViewerRole) {
      this.error = 'Viewer role is read-only and cannot delete scenarios.';
      return;
    }

    if (!confirm(`Delete scenario "${this.scenario?.title}"?`)) return;
    this.scenarioService.deleteScenario(this.scenarioId).subscribe({
      next: (response) => {
        if (response.status === 204 || response.status === 200) {
          this.router.navigate(['/scenarios']);
        }
      },
      error: (err) => {
        alert(err.error?.fail_Messages || 'Error deleting scenario');
      }
    });
  }

  runScenario(isHeadless: boolean = true): void {
    if (!this.canRunScenarioTests) {
      this.error = 'Only Admin and Tester roles can run scenario tests.';
      return;
    }
    if (!this.scenarioId) return;

    this.aiRunning = true;
    this.aiError = '';
    this.lastAiExecutionId = '';
    this.activeTab = 'ai';

    // Register the run with the global notification service so it survives
    // navigation; the topbar bell will show the running indicator and surface
    // the result when it finishes.
    const notif = this.runNotifications.runScenario(
      { scenarioId: this.scenarioId, isHeadless },
      this.scenario?.title || 'Scenario',
      this.scenario?.featureName,
    );

    // Locally mirror the notification's state so the inline AI tab still
    // reflects progress when the user stays on this page.
    this.runNotifications.runs$
      .pipe(takeUntil(this.destroy$))
      .subscribe((runs) => {
        const r = runs.find(x => x.id === notif.id);
        if (!r) return;
        if (r.state === 'running') return;
        this.aiRunning = false;
        if (r.state === 'passed') {
          this.lastAiExecutionId = r.executionId || '';
        } else {
          this.aiError = r.errorMessage || 'AI test run failed.';
        }
      });
  }

  exportScenario(): void {
    this.scenarioService.exportScenario(this.scenarioId).subscribe({
      next: (response) => {
        if (response.status === 200 && response.resultat) {
          const blob = new Blob([response.resultat], { type: 'text/plain' });
          const url = window.URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = `${this.scenario?.title || 'scenario'}.feature`;
          a.click();
          window.URL.revokeObjectURL(url);
        }
      },
      error: (err) => {
        alert(err.error?.fail_Messages || 'Error exporting');
      }
    });
  }

  selectVersion(versionNumber: number): void {
    this.selectedVersion = versionNumber;
  }

  getSelectedVersionContent(): string {
    if (!this.scenario || !this.selectedVersion) return '';
    if (this.selectedVersion === this.scenario.currentVersion) return this.scenario.gherkinContent;
    const version = this.scenario.versions.find(v => v.versionNumber === this.selectedVersion);
    return version?.gherkinContent || '';
  }

  getGherkinLines(): { lineNumber: number; content: string }[] {
    const content = this.getSelectedVersionContent();
    if (!content) return [];
    return content.split('\n').map((line, index) => ({
      lineNumber: index + 1,
      content: line
    }));
  }

  isKeywordLine(line: string): boolean {
    const trimmed = line.trim();
    return /^(Feature:|Scenario:|Given |When |Then |And |But |Examples:)/.test(trimmed);
  }

  getLineKeyword(line: string): string {
    const trimmed = line.trim();
    const match = trimmed.match(/^(Feature:|Scenario:|Given|When|Then|And|But|Examples:)/);
    return match ? match[1] : '';
  }

  getKeywordClass(keyword: string): string {
    const map: Record<string, string> = {
      'Feature:': 'text-blue-400',
      'Scenario:': 'text-blue-400',
      'Given': 'text-blue-400',
      'When': 'text-purple-400',
      'Then': 'text-green-400',
      'And': 'text-yellow-400',
      'But': 'text-orange-400',
      'Examples:': 'text-cyan-400'
    };
    return map[keyword] || 'text-slate-300';
  }

  getStepColor(stepType: StepType): string {
    const colors: { [key in StepType]: string } = {
      [StepType.Given]: 'text-blue-600',
      [StepType.When]: 'text-purple-600',
      [StepType.Then]: 'text-green-600',
      [StepType.And]: 'text-slate-600',
      [StepType.But]: 'text-orange-600'
    };
    return colors[stepType] || 'text-slate-600';
  }

  getStatusColor(status: string): string {
    const colors: { [key: string]: string } = {
      'Draft': 'bg-surface-container-high text-on-surface-variant',
      'Active': 'bg-tertiary-fixed text-on-tertiary-fixed-variant',
      'Archived': 'bg-amber-100 text-amber-700',
      'Deprecated': 'bg-error-container text-on-error-container'
    };
    return colors[status] || 'bg-slate-100 text-slate-700';
  }

  navigateBack(): void {
    this.router.navigate(['/scenarios']);
  }
}
