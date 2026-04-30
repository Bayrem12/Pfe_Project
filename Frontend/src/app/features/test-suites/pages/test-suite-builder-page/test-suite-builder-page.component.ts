import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CdkDragDrop, DragDropModule, moveItemInArray, transferArrayItem } from '@angular/cdk/drag-drop';
import { Subject, takeUntil } from 'rxjs';
import { TestSuiteService } from '../../services/test-suite.service';
import { ScenarioService } from '../../services/scenario.service';
import { TestSuiteWithCasesDto, TestSuiteScenarioDto, ScenarioDto, ScenarioStatus } from '../../models/test-suite.model';
import { AuthService } from '../../../../core/services/auth.service';
import { IaAgentService } from '../../../../core/services/ia-agent.service';
import { RunNotificationsService } from '../../../../core/services/run-notifications.service';

@Component({
  selector: 'app-test-suite-builder-page',
  standalone: true,
  imports: [CommonModule, FormsModule, DragDropModule],
  templateUrl: './test-suite-builder-page.component.html',
  styleUrls: ['./test-suite-builder-page.component.scss']
})
export class TestSuiteBuilderPageComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private testSuiteService = inject(TestSuiteService);
  private scenarioService = inject(ScenarioService);
  private authService = inject(AuthService);
  private iaAgentService = inject(IaAgentService);
  private runNotifications = inject(RunNotificationsService);
  private destroy$ = new Subject<void>();

  suiteId = '';
  suite: TestSuiteWithCasesDto | null = null;
  availableScenarios: ScenarioDto[] = [];
  filteredAvailable: ScenarioDto[] = [];
  suiteScenarios: TestSuiteScenarioDto[] = [];

  suiteName = '';
  suiteDescription = '';
  searchTerm = '';
  statusFilter: 'all' | 'active' | 'draft' | 'archived' = 'all';

  loading = true;
  saving = false;
  running = false;
  errorMessage = '';
  successMessage = '';
  addingScenarioId: string | null = null;
  removingScenarioId: string | null = null;
  private scenarioLookup = new Map<string, ScenarioDto>();

  get isViewerRole(): boolean {
    return this.authService.isProjectReadOnly();
  }

  get canRunTests(): boolean {
    return this.authService.canRunTests();
  }

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntil(this.destroy$)).subscribe(params => {
      this.suiteId = params.get('suiteId') || '';
      if (this.suiteId) {
        this.loadData();
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadData(): void {
    this.loading = true;
    this.errorMessage = '';

    this.testSuiteService.getTestSuiteWithCases(this.suiteId).subscribe({
      next: (suite) => {
        this.suite = suite;
        this.suiteName = suite.name;
        this.suiteDescription = suite.description;
        this.suiteScenarios = [...suite.scenarios].sort((a, b) => a.displayOrder - b.displayOrder);

        this.scenarioService.getScenariosByProject(suite.projectId).subscribe({
          next: (scenarios) => {
            this.availableScenarios = scenarios;
            this.scenarioLookup = new Map(scenarios.map(scenario => [scenario.id, scenario]));
            this.filterAvailableScenarios();
            this.loading = false;
          },
          error: () => {
            this.availableScenarios = [];
            this.filteredAvailable = [];
            this.scenarioLookup.clear();
            this.loading = false;
          }
        });
      },
      error: () => {
        this.errorMessage = 'Failed to load test suite.';
        this.loading = false;
      }
    });
  }

  filterAvailableScenarios(): void {
    const suiteScenarioIds = new Set(this.suiteScenarios.map(s => s.scenarioId));
    let available = this.availableScenarios.filter(s => !suiteScenarioIds.has(s.id));

    if (this.statusFilter !== 'all') {
      const selectedStatus = this.statusFilter === 'active'
        ? ScenarioStatus.Active
        : this.statusFilter === 'draft'
          ? ScenarioStatus.Draft
          : ScenarioStatus.Archived;
      available = available.filter(s => s.status === selectedStatus);
    }

    if (this.searchTerm) {
      const term = this.searchTerm.toLowerCase();
      available = available.filter(s =>
        s.title.toLowerCase().includes(term) ||
        s.featureName?.toLowerCase().includes(term) ||
        s.description?.toLowerCase().includes(term)
      );
    }

    this.filteredAvailable = available;
  }

  setStatusFilter(filter: 'all' | 'active' | 'draft' | 'archived'): void {
    this.statusFilter = filter;
    this.filterAvailableScenarios();
  }

  onSearchChange(value: string): void {
    this.searchTerm = value;
    this.filterAvailableScenarios();
  }

  addScenario(scenario: ScenarioDto): void {
    if (this.isViewerRole) {
      this.errorMessage = 'Viewer role is read-only and cannot modify test suites.';
      return;
    }

    if (this.addingScenarioId) return;
    this.addingScenarioId = scenario.id;

    this.testSuiteService.addScenarioToSuite(this.suiteId, scenario.id).subscribe({
      next: () => {
        const newItem: TestSuiteScenarioDto = {
          id: crypto.randomUUID(),
          scenarioId: scenario.id,
          scenarioTitle: scenario.title,
          scenarioDescription: scenario.description,
          displayOrder: this.suiteScenarios.length + 1
        };
        this.suiteScenarios.push(newItem);
        this.filterAvailableScenarios();
        this.addingScenarioId = null;
        this.showSuccess('Scenario added to suite.');
      },
      error: () => {
        this.errorMessage = 'Failed to add scenario.';
        this.addingScenarioId = null;
      }
    });
  }

  removeScenario(item: TestSuiteScenarioDto): void {
    if (this.isViewerRole) {
      this.errorMessage = 'Viewer role is read-only and cannot modify test suites.';
      return;
    }

    if (this.removingScenarioId) return;
    this.removingScenarioId = item.scenarioId;

    this.testSuiteService.removeScenarioFromSuite(this.suiteId, item.scenarioId).subscribe({
      next: () => {
        this.suiteScenarios = this.suiteScenarios.filter(s => s.scenarioId !== item.scenarioId);
        this.reindex();
        this.filterAvailableScenarios();
        this.removingScenarioId = null;
        this.showSuccess('Scenario removed from suite.');
      },
      error: () => {
        this.errorMessage = 'Failed to remove scenario.';
        this.removingScenarioId = null;
      }
    });
  }

  onDrop(event: CdkDragDrop<any[]>): void {
    if (this.isViewerRole) {
      this.errorMessage = 'Viewer role is read-only and cannot reorder test suites.';
      return;
    }

    if (event.previousContainer === event.container) {
      // Reorder within suite list
      moveItemInArray(this.suiteScenarios, event.previousIndex, event.currentIndex);
      this.reindex();
    } else {
      // Dragged from available list to suite list
      const scenario: ScenarioDto = event.item.data;
      if (scenario) {
        this.addScenario(scenario);
      }
    }
  }

  private reindex(): void {
    this.suiteScenarios.forEach((s, i) => s.displayOrder = i + 1);
  }

  saveSuite(): void {
    if (this.isViewerRole) {
      this.errorMessage = 'Viewer role is read-only and cannot save test suites.';
      return;
    }

    if (!this.suite || !this.suiteName.trim()) return;
    this.saving = true;

    this.testSuiteService.updateTestSuite(this.suiteId, {
      id: this.suiteId,
      name: this.suiteName.trim(),
      description: this.suiteDescription
    }).subscribe({
      next: () => {
        this.saving = false;
        this.showSuccess('Suite saved successfully.');
      },
      error: () => {
        this.errorMessage = 'Failed to save suite.';
        this.saving = false;
      }
    });
  }

  cancel(): void {
    this.router.navigate(['/test-suites']);
  }

  runSuite(isHeadless: boolean = true): void {
    if (!this.canRunTests) {
      this.errorMessage = 'Only Owner and Tester roles can run test suites.';
      return;
    }

    if (this.suiteScenarios.length === 0) return;
    this.running = true;
    this.errorMessage = '';
    this.successMessage = '';

    // Register the suite run with the global notification service. The user
    // can navigate away — the run continues and the topbar will surface the
    // completion event.
    const notif = this.runNotifications.runSuite(
      { testSuiteId: this.suiteId, isHeadless },
      this.suiteName || 'Test Suite',
      `${this.suiteScenarios.length} scenarios`,
    );

    this.runNotifications.runs$
      .pipe(takeUntil(this.destroy$))
      .subscribe((runs) => {
        const r = runs.find(x => x.id === notif.id);
        if (!r || r.state === 'running') return;
        this.running = false;
        if (r.state === 'passed' && r.executionId) {
          this.showSuccess('Test suite execution completed. Opening report…');
          this.router.navigate(['/test-runs', r.executionId]);
        } else {
          this.errorMessage = r.errorMessage || 'Failed to run the test suite.';
        }
      });
  }

  formatOrder(index: number): string {
    return String(index + 1).padStart(2, '0');
  }

  get totalProjectScenarios(): number {
    return this.availableScenarios.length;
  }

  get availableCount(): number {
    return this.totalProjectScenarios - this.suiteScenarios.length;
  }

  get suiteStepCount(): number {
    return this.suiteScenarios.reduce((total, item) => total + this.getScenarioStepCount(item.scenarioId), 0);
  }

  get hasChanges(): boolean {
    if (!this.suite) {
      return false;
    }

    return this.suiteName.trim() !== this.suite.name || this.suiteDescription !== this.suite.description;
  }

  getScenarioFeatureName(scenarioId: string): string {
    return this.scenarioLookup.get(scenarioId)?.featureName || 'Feature';
  }

  getScenarioStepCount(scenarioId: string): number {
    return this.scenarioLookup.get(scenarioId)?.stepCount || 0;
  }

  getScenarioStatusLabel(status: ScenarioStatus | null): string {
    switch (status) {
      case ScenarioStatus.Active:
        return 'Active';
      case ScenarioStatus.Draft:
        return 'Draft';
      case ScenarioStatus.Archived:
        return 'Archived';
      default:
        return 'In Suite';
    }
  }

  getScenarioStatusClass(status: ScenarioStatus | null): string {
    switch (status) {
      case ScenarioStatus.Active:
        return 'bg-emerald-50 text-emerald-700 border border-emerald-100';
      case ScenarioStatus.Draft:
        return 'bg-amber-50 text-amber-700 border border-amber-100';
      case ScenarioStatus.Archived:
        return 'bg-slate-100 text-slate-500 border border-slate-200';
      default:
        return 'bg-blue-50 text-blue-700 border border-blue-100';
    }
  }

  getScenarioStatus(scenarioId: string): ScenarioStatus | null {
    return this.scenarioLookup.get(scenarioId)?.status ?? null;
  }

  private showSuccess(msg: string): void {
    this.successMessage = msg;
    setTimeout(() => this.successMessage = '', 3000);
  }
}
