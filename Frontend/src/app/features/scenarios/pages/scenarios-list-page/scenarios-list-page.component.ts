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

@Component({
  selector: 'app-scenarios-list-page',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
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

  statusOptions = [
    { value: '', label: 'Status: All' },
    { value: ScenarioStatus.Draft, label: 'Draft' },
    { value: ScenarioStatus.Active, label: 'Active' },
    { value: ScenarioStatus.Archived, label: 'Archived' },
    { value: ScenarioStatus.Deprecated, label: 'Deprecated' }
  ];

  constructor(
    private scenarioService: ScenarioService,
    private projectService: ProjectService,
    private authService: AuthService,
    private breadcrumbService: BreadcrumbService,
    private router: Router,
    private route: ActivatedRoute
  ) {}

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
        this.error = err.error?.fail_Messages || 'Failed to load scenarios.';
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
  bulkDelete(): void {
    if (this.isViewerRole) {
      this.error = 'Viewer role is read-only and cannot delete scenarios.';
      return;
    }

    if (!confirm(`Delete ${this.selectedIds.size} selected scenarios?`)) return;
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
  bulkRun(): void {
    if (!this.canRunScenarioTests) {
      this.error = 'Only Admin and Tester roles can run scenario tests.';
      return;
    }

    if (!confirm(`Run ${this.selectedIds.size} selected scenarios?`)) return;
    // Placeholder: no run API yet — just show confirmation
    alert(`${this.selectedIds.size} scenarios queued for execution.`);
  }

  bulkAddTags(): void {
    if (this.isViewerRole) {
      this.error = 'Viewer role is read-only and cannot update scenario tags.';
      return;
    }

    const tagInput = prompt('Enter tags to add (comma-separated):');
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
      this.error = 'Only Admin and Tester roles can create scenarios.';
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
      this.error = 'Viewer role is read-only and cannot edit scenarios.';
      return;
    }

    this.router.navigate(['/scenarios', id, 'edit']);
  }

  runScenario(scenario: ScenarioDto, event: Event): void {
    event.stopPropagation();

    if (!this.canRunScenarioTests) {
      this.error = 'Only Admin and Tester roles can run scenario tests.';
      return;
    }

    // Navigate to the detail page with AI Analysis tab pre-selected
    this.router.navigate(['/scenarios', scenario.id], { fragment: 'ai' });
  }

  deleteScenario(scenario: ScenarioDto, event: Event): void {
    event.stopPropagation();

    if (this.isViewerRole) {
      this.error = 'Viewer role is read-only and cannot delete scenarios.';
      return;
    }

    if (!confirm(`Delete scenario "${scenario.title}"?`)) return;
    this.scenarioService.deleteScenario(scenario.id).subscribe({
      next: () => {
        this.scenarios = this.scenarios.filter(s => s.id !== scenario.id);
        this.applyFilters();
      },
      error: (err) => {
        this.error = err.error?.fail_Messages || 'Failed to delete scenario.';
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
      [ScenarioStatus.Draft]: 'DRAFT',
      [ScenarioStatus.Active]: 'ACTIVE',
      [ScenarioStatus.Archived]: 'ARCHIVED',
      [ScenarioStatus.Deprecated]: 'DEPRECATED'
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