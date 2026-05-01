// src/app/features/scenarios/pages/scenarios-list-page/scenarios-list-page.component.ts
import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ScenarioService } from '../../../../core/services/scenario.service';
import { ScenarioDto, ScenarioStatus } from '../../../../core/models/scenario.model';
import { ProjectService } from '../../../../core/services/project.service';
import { AuthService } from '../../../../core/services/auth.service';
import { Project } from '../../../../core/models/project.model';
import { BreadcrumbService } from '../../../../core/services/breadcrumb.service';
import { ResponseHttp } from '../../../../core/models/response-http.model';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { TranslatePipe } from '../../../../shared/pipes/translate.pipe';
import { TranslationService } from '../../../../core/services/translation.service';
import { ConfirmService } from '../../../../core/services/confirm.service';

@Component({
  selector: 'app-scenarios-list-page',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, TranslatePipe],
  templateUrl: './scenarios-list-page.component.html',
  styleUrl: './scenarios-list-page.component.scss'
})
export class ScenariosListPageComponent implements OnInit, OnDestroy {
  scenarios: ScenarioDto[] = [];
  filteredScenarios: ScenarioDto[] = [];
  pagedScenarios: ScenarioDto[] = [];

  // Context
  projectId: string = '';
  projectName: string = '';
  featureId: string = '';
  featureName: string = '';

  // Filters
  searchTerm: string = '';
  selectedStatus: ScenarioStatus | '' = '';
  selectedModule: string = '';
  selectedTagFilters: string[] = [];
  showTagsDropdown = false;

  // Extracted filter options
  uniqueModules: string[] = [];
  uniqueTags: string[] = [];

  // Selection
  selectedIds = new Set<string>();
  allSelected = false;

  // Pagination
  currentPage = 1;
  rowsPerPage = 10;
  totalPages = 1;

  // Project selection (when no projectId provided)
  projects: Project[] = [];
  loadingProjects = false;
  showProjectPicker = false;

  // AI Insight
  showAiInsight = true;

  loading = false;
  error = '';
  private destroy$ = new Subject<void>();

  get canCreateScenario(): boolean {
    return this.authService.canCreateScenarios();
  }

  get canRunScenarioTests(): boolean {
    return this.authService.canRunTests();
  }

  get isViewerRole(): boolean {
    return this.authService.isProjectReadOnly();
  }

  constructor(
    private scenarioService: ScenarioService,
    private projectService: ProjectService,
    private authService: AuthService,
    private breadcrumbService: BreadcrumbService,
    private translationService: TranslationService,
    private confirmService: ConfirmService,
    private router: Router,
    private route: ActivatedRoute
  ) {}

  get statusOptions(): Array<{ value: ScenarioStatus | ''; label: string }> {
    return [
      { value: '', label: this.t('scenario.list.statusAll') },
      { value: ScenarioStatus.Draft, label: this.t('scenario.list.status.draft') },
      { value: ScenarioStatus.Active, label: this.t('scenario.list.status.active') },
      { value: ScenarioStatus.Archived, label: this.t('scenario.list.status.archived') },
      { value: ScenarioStatus.Deprecated, label: this.t('scenario.list.status.deprecated') }
    ];
  }

  private t(key: string, ...args: string[]): string {
    return this.translationService.t(key, ...args);
  }

  private normalizeTag(tag: string): string {
    return (tag || '').replace(/^@/, '').trim();
  }

  getDisplayTags(tags?: string[] | null): string[] {
    const seen = new Set<string>();
    const result: string[] = [];

    for (const tag of tags ?? []) {
      const normalized = this.normalizeTag(tag);
      const key = normalized.toLowerCase();

      if (!normalized || seen.has(key)) {
        continue;
      }

      seen.add(key);
      result.push(normalized);
    }

    return result;
  }

  getTestStatusLabel(testStatus?: string | null): string {
    if ((testStatus || '').toLowerCase() === 'passed') {
      return this.t('label.passed');
    }

    if ((testStatus || '').toLowerCase() === 'failed') {
      return this.t('label.failed');
    }

    return this.t('label.notTested');
  }

  ngOnInit(): void {
    this.route.queryParams.pipe(takeUntil(this.destroy$)).subscribe(params => {
      this.projectId  = params['projectId']  || '';
      this.projectName = params['projectName'] || '';
      this.featureId  = params['featureId']  || '';
      this.featureName = params['featureName'] || '';

      if (this.projectId || this.featureId) {
        this.showProjectPicker = false;
        this.updateBreadcrumb();
        this.loadScenarios();
      } else {
        this.showProjectPicker = true;
        this.breadcrumbService.setMultiple([{ label: 'Scenarios' }]);
        this.loadProjects();
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.breadcrumbService.clear();
  }

  updateBreadcrumb(): void {
    const crumbs = [
      { label: this.projectName || 'Project', url: `/projects/${this.projectId}` },
      { label: 'Scenarios' }
    ];
    this.breadcrumbService.setMultiple(crumbs);
  }

  loadProjects(): void {
    this.loadingProjects = true;
    this.authService.currentUser$.pipe(takeUntil(this.destroy$)).subscribe(user => {
      const userId = user?.id || '';
      this.projectService.getProjects(userId).subscribe({
        next: (response: ResponseHttp) => {
          const data = response?.resultat;
          if (Array.isArray(data)) {
            this.projects = data;
          } else if (data && Array.isArray(data.items)) {
            this.projects = data.items;
          } else if (data && Array.isArray(data.Items)) {
            this.projects = data.Items;
          } else {
            this.projects = [];
          }
          this.loadingProjects = false;
        },
        error: () => {
          this.projects = [];
          this.loadingProjects = false;
        }
      });
    });
  }

  selectProject(project: Project): void {
    this.router.navigate(['/scenarios'], {
      queryParams: { projectId: project.id, projectName: project.name }
    });
  }

  get contextLabel(): string {
    if (this.featureName) return `Feature: ${this.featureName}`;
    if (this.projectName) return `Project: ${this.projectName}`;
    return 'All Scenarios';
  }

  loadScenarios(): void {
    this.loading = true;
    this.error = '';
    const qProjectId = this.projectId || '';
    const status = this.selectedStatus || undefined;

    this.scenarioService.getScenarios(qProjectId, this.searchTerm, status).subscribe({
      next: (response) => {
        if (response.status === 200 && response.resultat) {
          this.scenarios = response.resultat;
        } else {
          this.scenarios = [];
        }
        this.extractFilterOptions();
        this.applyFilters();
        this.loading = false;
      },
      error: (err) => {
        this.error = err.error?.fail_Messages || this.t('scenario.list.error.loadFailed');
        this.loading = false;
      }
    });
  }

  extractFilterOptions(): void {
    const moduleSet = new Set<string>();
    const tagSet = new Set<string>();
    this.scenarios.forEach(s => {
      if (s.moduleName) moduleSet.add(s.moduleName);
      if (s.tags) s.tags.forEach(t => tagSet.add(t));
    });
    this.uniqueModules = Array.from(moduleSet).sort();
    this.uniqueTags = Array.from(tagSet).sort();
  }

  applyFilters(): void {
    this.filteredScenarios = this.scenarios.filter(s => {
      const matchesFeature = !this.featureId || s.featureId === this.featureId;
      const matchesSearch = !this.searchTerm ||
        s.title.toLowerCase().includes(this.searchTerm.toLowerCase()) ||
        (s.description || '').toLowerCase().includes(this.searchTerm.toLowerCase());
      const matchesStatus = !this.selectedStatus || s.status === this.selectedStatus;
      const matchesModule = !this.selectedModule || s.moduleName === this.selectedModule;
      const matchesTags = this.selectedTagFilters.length === 0 ||
        (s.tags && this.selectedTagFilters.every(t => s.tags!.includes(t)));
      return matchesFeature && matchesSearch && matchesStatus && matchesModule && matchesTags;
    });
    this.currentPage = 1;
    this.updatePagination();
  }

  updatePagination(): void {
    this.totalPages = Math.max(1, Math.ceil(this.filteredScenarios.length / this.rowsPerPage));
    if (this.currentPage > this.totalPages) this.currentPage = this.totalPages;
    const start = (this.currentPage - 1) * this.rowsPerPage;
    this.pagedScenarios = this.filteredScenarios.slice(start, start + this.rowsPerPage);
  }

  get paginationStart(): number { return (this.currentPage - 1) * this.rowsPerPage + 1; }
  get paginationEnd(): number { return Math.min(this.currentPage * this.rowsPerPage, this.filteredScenarios.length); }

  get pageNumbers(): number[] {
    const pages: number[] = [];
    if (this.totalPages <= 5) {
      for (let i = 1; i <= this.totalPages; i++) pages.push(i);
    } else {
      pages.push(1, 2, 3);
      if (this.totalPages > 4) pages.push(-1); // ellipsis
      pages.push(this.totalPages);
    }
    return pages;
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages) return;
    this.currentPage = page;
    this.updatePagination();
  }

  onRowsPerPageChange(): void {
    this.currentPage = 1;
    this.updatePagination();
  }

  onSearchChange(): void { this.applyFilters(); }
  onStatusChange(): void { this.applyFilters(); }
  onModuleChange(): void { this.applyFilters(); }

  toggleTagFilter(tag: string): void {
    const idx = this.selectedTagFilters.indexOf(tag);
    if (idx >= 0) {
      this.selectedTagFilters.splice(idx, 1);
    } else {
      this.selectedTagFilters.push(tag);
    }
    this.applyFilters();
  }

  isTagFilterSelected(tag: string): boolean {
    return this.selectedTagFilters.includes(tag);
  }

  clearAllFilters(): void {
    this.searchTerm = '';
    this.selectedStatus = '';
    this.selectedModule = '';
    this.selectedTagFilters = [];
    this.showTagsDropdown = false;
    this.applyFilters();
  }

  // Selection
  toggleSelectAll(): void {
    if (this.allSelected) {
      this.selectedIds.clear();
      this.allSelected = false;
    } else {
      this.pagedScenarios.forEach(s => this.selectedIds.add(s.id));
      this.allSelected = true;
    }
  }

  toggleSelect(id: string): void {
    if (this.selectedIds.has(id)) {
      this.selectedIds.delete(id);
    } else {
      this.selectedIds.add(id);
    }
    this.allSelected = this.pagedScenarios.every(s => this.selectedIds.has(s.id));
  }

  isSelected(id: string): boolean {
    return this.selectedIds.has(id);
  }

  clearSelection(): void {
    this.selectedIds.clear();
    this.allSelected = false;
  }

  // Bulk Actions
  async bulkDelete(): Promise<void> {
    if (this.isViewerRole) {
      this.error = this.t('scenario.list.error.deleteReadonly');
      return;
    }

    const ok = await this.confirmService.open({
      title: this.t('scenario.list.confirm.bulkDelete', String(this.selectedIds.size)),
      description: this.t('delete.dialog.defaultDesc'),
      confirmLabel: this.t('action.delete'),
      cancelLabel: this.t('action.cancel'),
    });
    if (!ok) return;
    const ids = Array.from(this.selectedIds);
    let completed = 0;
    ids.forEach(id => {
      this.scenarioService.deleteScenario(id).subscribe({
        next: () => {
          completed++;
          if (completed === ids.length) {
            this.scenarios = this.scenarios.filter(s => !this.selectedIds.has(s.id));
            this.selectedIds.clear();
            this.allSelected = false;
            this.applyFilters();
          }
        }
      });
    });
  }

  bulkExport(): void {
    this.selectedIds.forEach(id => {
      const scenario = this.scenarios.find(s => s.id === id);
      if (scenario) {
        this.scenarioService.exportScenario(id).subscribe({
          next: (response) => {
            if (response.status === 200 && response.resultat) {
              const blob = new Blob([response.resultat], { type: 'text/plain' });
              const url = window.URL.createObjectURL(blob);
              const a = document.createElement('a');
              a.href = url;
              a.download = `${scenario.title}.feature`;
              a.click();
              window.URL.revokeObjectURL(url);
            }
          }
        });
      }
    });
  }

  // Bulk Actions
  async bulkRun(): Promise<void> {
    if (!this.canRunScenarioTests) {
      this.error = 'Only Owner and Tester roles can run scenario tests.';
      return;
    }

    const ok = await this.confirmService.open({
      title: this.t('scenario.list.confirm.bulkRun', String(this.selectedIds.size)),
      confirmLabel: this.t('action.run'),
      cancelLabel: this.t('action.cancel'),
      variant: 'primary',
    });
    if (!ok) return;
    // Placeholder: no run API yet — just show confirmation
    alert(this.t('scenario.list.alert.runQueued', String(this.selectedIds.size)));
  }

  bulkAddTags(): void {
    if (this.isViewerRole) {
      this.error = this.t('scenario.list.error.tagsReadonly');
      return;
    }

    const tagInput = prompt(this.t('scenario.list.prompt.addTags'));
    if (!tagInput) return;
    const newTags = tagInput.split(',').map(t => t.trim()).filter(t => t.length > 0);
    if (newTags.length === 0) return;
    // Update locally (backend update would require a dedicated endpoint)
    this.scenarios.forEach(s => {
      if (this.selectedIds.has(s.id)) {
        if (!s.tags) s.tags = [];
        newTags.forEach(tag => {
          if (!s.tags!.includes(tag)) s.tags!.push(tag);
        });
      }
    });
    this.extractFilterOptions();
    this.applyFilters();
    this.clearSelection();
  }

  navigateToCreate(): void {
    if (!this.canCreateScenario) {
      this.error = 'Only Owner and Tester roles can create scenarios.';
      return;
    }

    this.router.navigate(['/scenarios/new'], {
      queryParams: {
        featureId: this.featureId || '',
        featureName: this.featureName || '',
        projectId: this.projectId || '',
        projectName: this.projectName || ''
      }
    });
  }

  navigateToDetail(id: string): void {
    this.router.navigate(['/scenarios', id]);
  }

  navigateToEdit(id: string, event: Event): void {
    event.stopPropagation();

    if (this.isViewerRole) {
      this.error = this.t('scenario.list.error.editReadonly');
      return;
    }

    this.router.navigate(['/scenarios', id, 'edit']);
  }

  runScenario(scenario: ScenarioDto, event: Event): void {
    event.stopPropagation();

    if (!this.canRunScenarioTests) {
      this.error = 'Only Owner and Tester roles can run scenario tests.';
      return;
    }

    // Navigate to the detail page with AI Analysis tab pre-selected
    this.router.navigate(['/scenarios', scenario.id], { fragment: 'ai' });
  }

  async deleteScenario(scenario: ScenarioDto, event: Event): Promise<void> {
    event.stopPropagation();

    if (this.isViewerRole) {
      this.error = this.t('scenario.list.error.deleteReadonly');
      return;
    }

    const ok = await this.confirmService.open({
      title: this.t('scenario.list.confirm.delete', scenario.title),
      description: this.t('delete.dialog.defaultDesc'),
      confirmLabel: this.t('action.delete'),
      cancelLabel: this.t('action.cancel'),
    });
    if (!ok) return;
    this.scenarioService.deleteScenario(scenario.id).subscribe({
      next: () => {
        this.scenarios = this.scenarios.filter(s => s.id !== scenario.id);
        this.applyFilters();
      },
      error: (err) => {
        this.error = err.error?.fail_Messages || this.t('scenario.list.error.deleteFailed');
      }
    });
  }

  navigateBack(): void {
    if (this.featureId) {
      this.router.navigate(['/features', this.featureId]);
    } else if (this.projectId) {
      this.router.navigate(['/projects', this.projectId]);
    } else {
      this.router.navigate(['/projects']);
    }
  }

  getStatusColor(status: ScenarioStatus): string {
    const map: Record<string, string> = {
      [ScenarioStatus.Draft]: 'bg-surface-container-high text-on-surface-variant',
      [ScenarioStatus.Active]: 'bg-tertiary-fixed/30 text-on-tertiary-fixed-variant',
      [ScenarioStatus.Archived]: 'bg-amber-100 text-amber-700',
      [ScenarioStatus.Deprecated]: 'bg-error-container text-on-error-container'
    };
    return map[status] || 'bg-surface-container text-on-surface-variant';
  }

  getStatusLabel(status: ScenarioStatus): string {
    const map: Record<string, string> = {
      [ScenarioStatus.Draft]: this.t('label.draft'),
      [ScenarioStatus.Active]: this.t('label.active'),
      [ScenarioStatus.Archived]: this.t('label.archived'),
      [ScenarioStatus.Deprecated]: this.t('label.deprecated')
    };
    return map[status] || status;
  }

  getStatusIcon(status: ScenarioStatus): string {
    const map: Record<string, string> = {
      [ScenarioStatus.Draft]: 'hourglass_empty',
      [ScenarioStatus.Active]: 'check_circle',
      [ScenarioStatus.Archived]: 'archive',
      [ScenarioStatus.Deprecated]: 'error'
    };
    return map[status] || 'info';
  }

  getTestStatusColor(testStatus: string): string {
    if (testStatus === 'Passed') return 'bg-tertiary-fixed/30 text-on-tertiary-fixed-variant';
    return 'bg-error-container text-on-error-container';
  }

}