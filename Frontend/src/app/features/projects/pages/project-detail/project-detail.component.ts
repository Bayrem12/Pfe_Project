import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { ProjectService } from '../../../../core/services/project.service';
import { toSlug } from '../../../../core/services/project.service';
import { FeatureService } from '../../../../core/services/feature.service';
import { ModuleService } from '../../../../core/services/module.service';
import { ScenarioService } from '../../../../core/services/scenario.service';
import { Project } from '../../../../core/models/project.model';
import { Module } from '../../../../core/models/module.model';
import { forkJoin, of } from 'rxjs';
import { AuthService } from '../../../../core/services/auth.service';
import { ModuleCreateComponent } from '../../module-create/module-create.component';
import { BreadcrumbService } from '../../../../core/services/breadcrumb.service';
import { TagService } from '../../../../core/services/tag.service';
import { ProjectTag } from '../../../../core/models/tag.model';
import { TagCreateModalComponent } from '../../tag-create-modal/tag-create-modal.component';
import { CreatedFeaturePayload, FeatureCreateModalComponent } from '../../feature-create-modal/feature-create-modal.component';
import { TranslatePipe } from '../../../../shared/pipes/translate.pipe';
import { ConfirmService } from '../../../../core/services/confirm.service';
import { TranslationService } from '../../../../core/services/translation.service';

@Component({
  selector: 'app-project-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, ModuleCreateComponent, TagCreateModalComponent, FeatureCreateModalComponent, TranslatePipe],
  templateUrl: './project-detail.component.html',
  styleUrl: './project-detail.component.scss'
})
export class ProjectDetailComponent implements OnInit, OnDestroy {

  get isViewerOrManager(): boolean {
    return this.currentRole === 'viewer' || this.currentRole === 'manager';
  }

  private static readonly STEP_KEYWORDS = ['GIVEN', 'WHEN', 'THEN', 'AND', 'BUT'];

  project: Project | null = null;
  isLoading = true;
  isHierarchyLoading = false;
  isScenarioLoading = false;
  isArchiving = false;
  currentRole = 'viewer';

  activeTab: 'overview' | 'modules' | 'scenarios' | 'executions' | 'ai-insights' = 'overview';
  projectId: string = '';
  showCreateModule = false;
  showCreateFeatureModal = false;
  featureCreateModuleId = '';
  featureCreateModuleName = '';
  showCreateTagModal = false;
  showDeleteTagModal = false;
  isTagsLoading = false;
  deletingTagId: string | null = null;
  deletingModuleId: string | null = null;
  deletingFeatureId: string | null = null;
  pendingDeleteTag: ProjectTag | null = null;
  tagsError = '';
  tagNotice: { type: 'success' | 'error'; text: string } | null = null;

  stats = {
    modules: 0,
    features: 0,
    scenarios: 0,
    lastExecution: 'il y a 0 h'
  };

  modules: ModuleDto[] = [];
  features: FeatureDto[] = [];
  scenarios: ScenarioListDto[] = [];
  tags: ProjectTag[] = [];

  projectHierarchy: HierarchyModule[] = [];
  selectedScenario: ScenarioListDto | null = null;
  selectedScenarioDetail: ScenarioDetailDto | null = null;
  gherkinLines: GherkinLine[] = [];
  recentExecutions: Array<{ date: string; status: 'passed' | 'failed' | 'neutral' }> = [];

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private projectService: ProjectService,
    private featureService: FeatureService,
    private moduleService: ModuleService,
    private scenarioService: ScenarioService,
    private authService: AuthService,
    private breadcrumbService: BreadcrumbService,
    private tagService: TagService,
    private confirmService: ConfirmService,
    private translationService: TranslationService
  ) {}

  private initialized = false;

  ngOnInit(): void {
    this.authService.currentUser$.subscribe(user => {
      this.currentRole = (user?.roles?.[0] || 'viewer').toLowerCase();

      if (this.initialized) return;

      const slug = this.route.snapshot.paramMap.get('slug') || '';
      const stateId = (history.state as any)?.projectId;

      if (stateId) {
        this.initialized = true;
        this.projectId = stateId;
        this.loadProject();
        this.loadProjectDataFromDatabase();
        this.loadTags();
      } else if (slug && user?.id) {
        this.initialized = true;
        this.projectService.getProjects(user.id).subscribe({
          next: (response: any) => {
            const data = response?.resultat ?? response;
            const projects: Project[] = Array.isArray(data) ? data
              : Array.isArray(data?.items) ? data.items
              : Array.isArray(data?.Items) ? data.Items
              : [];
            const match = projects.find((p: Project) => toSlug(p.name) === slug);
            if (match) {
              this.projectId = match.id;
              this.loadProject();
              this.loadProjectDataFromDatabase();
              this.loadTags();
            } else {
              this.router.navigate(['/projects']);
            }
          },
          error: () => this.router.navigate(['/projects'])
        });
      }
    });
  }

  loadProject(): void {
    this.isLoading = true;
    this.projectService.getProjectById(this.projectId).subscribe({
      next: (response) => {
        this.project = response.resultat;
        if (this.project?.name) {
          this.breadcrumbService.set(this.project.name.toUpperCase(), `/projects/${toSlug(this.project.name)}`);
        }
        this.isLoading = false;
      },
      error: (error) => {
        console.error('Erreur lors du chargement du projet:', error);
        this.isLoading = false;
      }
    });
  }

  private loadProjectDataFromDatabase(): void {
    this.isHierarchyLoading = true;

    // ← On utilise maintenant ModuleService au lieu de ProjectService
    this.moduleService.getModulesByProjectId(this.projectId).subscribe({
      next: (response) => {
        const data = response?.resultat ?? response;
        this.modules = this.normalizeArray<ModuleDto>(data)
          .sort((a, b) => (a.displayOrder ?? 0) - (b.displayOrder ?? 0));
        this.stats.modules = this.modules.length;

        if (!this.modules.length) {
          this.features = [];
          this.scenarios = [];
          this.rebuildHierarchy();
          this.updateStats();
          this.isHierarchyLoading = false;
          return;
        }

        const featureCalls = this.modules.map((module) =>
          this.featureService.getFeaturesByModule(module.id)
        );

        forkJoin(featureCalls.length ? featureCalls : [of([])]).subscribe({
          next: (featureResponses) => {
            this.features = featureResponses
              .flatMap((resp) => this.normalizeArray<FeatureDto>(resp))
              .sort((a, b) => (a.displayOrder ?? 0) - (b.displayOrder ?? 0));

            this.loadScenarios();
          },
          error: (error) => {
            console.error('Erreur lors du chargement des features:', error);
            this.features = [];
            this.scenarios = [];
            this.rebuildHierarchy();
            this.updateStats();
            this.isHierarchyLoading = false;
          }
        });
      },
      error: (error) => {
        console.error('Erreur lors du chargement des modules:', error);
        this.modules = [];
        this.features = [];
        this.scenarios = [];
        this.rebuildHierarchy();
        this.updateStats();
        this.isHierarchyLoading = false;
      }
    });
  }

  private loadScenarios(): void {
    this.scenarioService.getScenarios(this.projectId).subscribe({
      next: (scenariosResponse) => {
        this.scenarios = this.normalizeArray<ScenarioListDto>(scenariosResponse);
        this.rebuildHierarchy();
        this.updateStats();
        this.isHierarchyLoading = false;

        if (this.scenarios.length > 0) {
          this.selectScenario(this.scenarios[0]);
        } else {
          this.selectedScenario = null;
          this.selectedScenarioDetail = null;
          this.gherkinLines = [];
        }
      },
      error: (error) => {
        console.error('Erreur lors du chargement des scénarios:', error);
        this.scenarios = [];
        this.rebuildHierarchy();
        this.updateStats();
        this.isHierarchyLoading = false;
      }
    });
  }

  private rebuildHierarchy(): void {
    this.projectHierarchy = this.modules.map((module, moduleIndex) => {
      const moduleFeatures = this.features
        .filter((feature) => feature.moduleId === module.id)
        .sort((a, b) => (a.displayOrder ?? 0) - (b.displayOrder ?? 0));

      const hierarchyFeatures: HierarchyFeature[] = moduleFeatures.map((feature, featureIndex) => {
        const featureScenarios = this.scenarios
          .filter((scenario) => scenario.featureId === feature.id)
          .sort((a, b) => a.title.localeCompare(b.title));

        return {
          id: feature.id,
          moduleId: module.id,
          name: feature.name,
          expanded: moduleIndex === 0 && featureIndex === 0,
          children: featureScenarios.map((scenario) => ({
            id: scenario.id,
            name: scenario.title,
            status: this.mapScenarioStatus(scenario.status),
            lastTestStatus: scenario.lastTestStatus
          }))
        };
      });

      return {
        id: module.id,
        name: module.name,
        expanded: moduleIndex === 0,
        children: hierarchyFeatures
      };
    });
  }

  private updateStats(): void {
    this.stats.modules = this.modules.length;
    this.stats.features = this.features.length;
    this.stats.scenarios = this.scenarios.length;

    const latestScenarioDate = this.scenarios
      .map((scenario) => this.toValidDate(scenario.updatedAt || scenario.createdAt))
      .filter((date): date is Date => !!date)
      .filter((date) => !isNaN(date.getTime()))
      .sort((a, b) => b.getTime() - a.getTime())[0];

    const fallbackDate = this.getProjectFallbackDate() ?? new Date();
    this.stats.lastExecution = this.getRelativeTime(latestScenarioDate ?? fallbackDate);

    this.recentExecutions = this.scenarios
      .slice(0, 5)
      .map((scenario) => ({
        date: this.formatShortDate(scenario.updatedAt || scenario.createdAt),
        status: this.mapScenarioStatus(scenario.status) === 'active'
          ? 'passed'
          : this.mapScenarioStatus(scenario.status) === 'archived'
            ? 'failed'
            : 'neutral'
      }));
  }

  ngOnDestroy(): void {
    this.breadcrumbService.clear();
  }

  // ─── Navigation vers création de module ───────────────────────────────────

  navigateToCreateModule(): void {
    this.showCreateModule = true;
  }

  navigateToCreateFeature(): void {
    if (!this.projectId || this.isViewerOrManager) return;

    const targetModule = this.modules[0];
    if (!targetModule) {
      this.showCreateModule = true;
      return;
    }

    this.featureCreateModuleId = targetModule.id;
    this.featureCreateModuleName = targetModule.name;
    this.showCreateFeatureModal = true;
  }

  openCreateTagModal(): void {
    this.showCreateTagModal = true;
    this.tagsError = '';
  }

  onModuleCreated(): void {
    this.showCreateModule = false;
    this.loadProjectDataFromDatabase();
  }

  onModuleCreateCancelled(): void {
    this.showCreateModule = false;
  }

  onFeatureCreated(payload: CreatedFeaturePayload): void {
    this.showCreateFeatureModal = false;
    this.featureCreateModuleId = '';
    this.featureCreateModuleName = '';
    this.loadProjectDataFromDatabase();
    this.tagNotice = {
      type: 'success',
      text: `Feature "${payload.name}" created successfully.`
    };
    this.dismissTagNoticeAfterDelay();
  }

  onFeatureCreateCancelled(): void {
    this.showCreateFeatureModal = false;
    this.featureCreateModuleId = '';
    this.featureCreateModuleName = '';
  }

  onTagCreated(tag: ProjectTag | null): void {
    this.showCreateTagModal = false;
    if (!tag) return;
    this.tagNotice = {
      type: 'success',
      text: `Tag “${tag.name}” created successfully.`
    };
    this.loadTags();
    this.dismissTagNoticeAfterDelay();
  }

  onTagCreateCancelled(): void {
    this.showCreateTagModal = false;
  }

  requestTagDeletion(tag: ProjectTag, event?: Event): void {
    event?.stopPropagation();

    if (this.isViewerOrManager || this.deletingTagId) {
      return;
    }

    this.pendingDeleteTag = tag;
    this.showDeleteTagModal = true;
  }

  cancelTagDeletion(): void {
    if (this.deletingTagId) {
      return;
    }

    this.showDeleteTagModal = false;
    this.pendingDeleteTag = null;
  }

  confirmTagDeletion(): void {
    const tag = this.pendingDeleteTag;
    if (!tag || this.isViewerOrManager || this.deletingTagId) {
      return;
    }

    this.deletingTagId = tag.id;
    this.tagsError = '';

    this.tagService.deleteTag(tag.id)
      .pipe(finalize(() => {
        this.deletingTagId = null;
      }))
      .subscribe({
        next: () => {
          this.tags = this.tags.filter(existingTag => existingTag.id !== tag.id);
          this.showDeleteTagModal = false;
          this.pendingDeleteTag = null;
          this.tagNotice = {
            type: 'success',
            text: `Tag “${tag.name}” deleted successfully.`
          };
          this.dismissTagNoticeAfterDelay();
        },
        error: (error) => {
          this.showDeleteTagModal = false;
          this.pendingDeleteTag = null;
          this.tagNotice = {
            type: 'error',
            text: error?.error?.fail_Messages || error?.error?.Fail_Messages || `Unable to delete tag “${tag.name}”.`
          };
          this.dismissTagNoticeAfterDelay();
        }
      });
  }

  // ─── Rechargement des modules après création ──────────────────────────────

  reloadModules(): void {
    this.isHierarchyLoading = true;
    this.loadProjectDataFromDatabase();
  }

  private loadTags(): void {
    this.isTagsLoading = true;
    this.tagsError = '';

    this.tagService.getProjectTags(this.projectId).subscribe({
      next: (response) => {
        this.tags = (response?.resultat ?? [])
          .slice()
          .sort((a, b) => a.name.localeCompare(b.name));
        this.isTagsLoading = false;
      },
      error: (error) => {
        console.error('Erreur lors du chargement des tags:', error);
        this.tagsError =
          error?.error?.fail_Messages ||
          error?.error?.Fail_Messages ||
          'Unable to load project tags.';
        this.tags = [];
        this.isTagsLoading = false;
      }
    });
  }

  // ─── Sélection de scénario ────────────────────────────────────────────────

  selectScenarioById(scenarioId: string): void {
    const existing = this.scenarios.find((scenario) => scenario.id === scenarioId);
    if (existing) {
      this.selectScenario(existing);
    }
  }

  selectScenario(scenario: ScenarioListDto): void {
    this.selectedScenario = scenario;
    this.loadScenarioDetail(scenario.id);
  }

  openScenarioInScenariosPage(scenario: {
    id: string;
    featureId?: string;
    featureName?: string;
  }): void {
    const queryParams: Record<string, string> = {};

    if (this.projectId) {
      queryParams['projectId'] = this.projectId;
    }

    if (this.project?.name) {
      queryParams['projectName'] = this.project.name;
    }

    if (scenario.featureId) {
      queryParams['featureId'] = scenario.featureId;
    }

    if (scenario.featureName) {
      queryParams['featureName'] = scenario.featureName;
    }

    this.router.navigate(['/scenarios'], { queryParams });
  }

  private loadScenarioDetail(scenarioId: string): void {
    this.isScenarioLoading = true;

    this.scenarioService.getScenarioById(scenarioId).subscribe({
      next: (response) => {
        this.selectedScenarioDetail = response?.resultat ?? null;
        this.gherkinLines = this.toGherkinLines(this.selectedScenarioDetail);
        this.isScenarioLoading = false;
      },
      error: (error) => {
        console.error('Erreur lors du chargement du détail scénario:', error);
        this.selectedScenarioDetail = null;
        this.gherkinLines = [];
        this.isScenarioLoading = false;
      }
    });
  }

  // ─── Arbre hiérarchique ───────────────────────────────────────────────────

  collapseAllNodes(): void {
    this.projectHierarchy.forEach((module) => {
      module.expanded = false;
      module.children.forEach((feature) => {
        feature.expanded = false;
      });
    });
  }

  toggleNode(node: any): void {
    node.expanded = !node.expanded;
  }

  async deleteModule(module: HierarchyModule, event?: Event): Promise<void> {
    event?.stopPropagation();

    if (this.isViewerOrManager || this.deletingModuleId || this.deletingFeatureId) {
      return;
    }

    const ok = await this.confirmService.open({
      title: this.translationService.t('project.detail.confirmDeleteModule', module.name),
      description: this.translationService.t('delete.dialog.defaultDesc'),
      confirmLabel: this.translationService.t('action.delete'),
      cancelLabel: this.translationService.t('action.cancel'),
    });
    if (!ok) {
      return;
    }

    this.deletingModuleId = module.id;

    this.moduleService.deleteModule(module.id)
      .pipe(finalize(() => {
        this.deletingModuleId = null;
      }))
      .subscribe({
        next: () => {
          this.loadProjectDataFromDatabase();
          this.tagNotice = {
            type: 'success',
            text: `Module "${module.name}" deleted successfully.`
          };
          this.dismissTagNoticeAfterDelay();
        },
        error: (error) => {
          this.tagNotice = {
            type: 'error',
            text:
              error?.error?.fail_Messages ||
              error?.error?.Fail_Messages ||
              `Unable to delete module "${module.name}".`
          };
          this.dismissTagNoticeAfterDelay();
        }
      });
  }

  async deleteFeature(feature: HierarchyFeature, event?: Event): Promise<void> {
    event?.stopPropagation();

    if (this.isViewerOrManager || this.deletingFeatureId || this.deletingModuleId) {
      return;
    }

    const ok = await this.confirmService.open({
      title: this.translationService.t('project.detail.confirmDeleteFeature', feature.name),
      description: this.translationService.t('delete.dialog.defaultDesc'),
      confirmLabel: this.translationService.t('action.delete'),
      cancelLabel: this.translationService.t('action.cancel'),
    });
    if (!ok) {
      return;
    }

    this.deletingFeatureId = feature.id;

    this.featureService.deleteFeature(feature.id)
      .pipe(finalize(() => {
        this.deletingFeatureId = null;
      }))
      .subscribe({
        next: () => {
          this.loadProjectDataFromDatabase();
          this.tagNotice = {
            type: 'success',
            text: `Feature "${feature.name}" deleted successfully.`
          };
          this.dismissTagNoticeAfterDelay();
        },
        error: (error) => {
          this.tagNotice = {
            type: 'error',
            text:
              error?.error?.fail_Messages ||
              error?.error?.Fail_Messages ||
              `Unable to delete feature "${feature.name}".`
          };
          this.dismissTagNoticeAfterDelay();
        }
      });
  }

  getTotalHierarchyCount(): number {
    return this.projectHierarchy.reduce(
      (total, module) => total + module.children.length, 0
    );
  }

  // ─── Navigation ──────────────────────────────────────────────────────────

  goBack(): void {
    this.router.navigate(['/projects']);
  }

  editProject(): void {
    if (!this.canManageProjects()) return;
    this.router.navigate(['/projects']);
  }

  async archiveProject(): Promise<void> {
    if (!this.canManageProjects()) return;
    if (!this.project || this.isArchiving) return;

    const ok = await this.confirmService.open({
      title: this.translationService.t('project.detail.confirmArchiveTitle'),
      description: this.translationService.t('project.detail.confirmArchiveDesc'),
      confirmLabel: this.translationService.t('project.detail.archive'),
      cancelLabel: this.translationService.t('action.cancel'),
      variant: 'primary',
    });
    if (!ok) return;

    this.isArchiving = true;
    this.projectService.updateProject(this.project.id, {
      projectId: this.project.id,
      name: this.project.name,
      description: this.project.description,
      url: this.project.url,
      isActive: false
    }).subscribe({
      next: () => {
        if (this.project) this.project.isActive = false;
        this.isArchiving = false;
      },
      error: (error) => {
        console.error('Erreur lors de l\'archivage du projet:', error);
        this.isArchiving = false;
      }
    });
  }

  setTab(tab: 'overview' | 'modules' | 'scenarios' | 'executions' | 'ai-insights'): void {
    this.activeTab = tab;
  }

  canManageProjects(): boolean {
    return this.currentRole === 'owner' || this.currentRole === 'tester';
  }

  // ─── Utilitaires affichage ────────────────────────────────────────────────

  getScenarioDisplayId(scenario: ScenarioListDto | null): string {
    if (!scenario?.id) return 'SC-000';
    const compact = scenario.id.replace(/-/g, '').slice(-3).toUpperCase();
    return `SC-${compact}`;
  }

  getExecutionTone(
    status: 'passed' | 'failed' | 'neutral',
    index: number
  ): 'mint' | 'rose' | 'slate' {
    if (status === 'passed') return index % 2 === 0 ? 'mint' : 'slate';
    if (status === 'failed') return 'rose';
    return 'slate';
  }

  getScenarioStatusLabel(status: number | string | undefined): string {
    const normalized = this.mapScenarioStatus(status ?? 0);
    if (normalized === 'active') return 'AUTOMATED';
    if (normalized === 'archived') return 'ARCHIVED';
    return 'DRAFT';
  }

  getScenarioStatusClass(status: number | string | undefined): string {
    return this.mapScenarioStatus(status ?? 0);
  }

  getTagBorderColor(color: string | null | undefined): string {
    return `${this.normalizeHexColor(color)}33`;
  }

  getTagBackgroundColor(color: string | null | undefined): string {
    return `${this.normalizeHexColor(color)}14`;
  }

  getTagDotColor(color: string | null | undefined): string {
    return this.normalizeHexColor(color);
  }

  getRelativeTime(date: Date): string {
    const diffMs = Date.now() - date.getTime();
    const diffHours = Math.floor(diffMs / (1000 * 60 * 60));

    if (diffHours < 24) return `il y a ${Math.max(1, diffHours)} h`;

    const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));
    if (diffDays < 30) return `il y a ${Math.max(1, diffDays)} j`;

    const diffMonths = Math.floor(diffDays / 30);
    if (diffMonths <= 12) return `il y a ${Math.max(1, diffMonths)} mois`;

    const diffYears = Math.floor(diffMonths / 12);
    return `il y a ${Math.max(1, diffYears)} an${Math.max(1, diffYears) > 1 ? 's' : ''}`;
  }

  formatShortDate(dateValue: string | Date): string {
    const date = this.toValidDate(dateValue);
    if (!date || isNaN(date.getTime())) return 'AUJ.';

    return new Intl.DateTimeFormat('en-US', {
      month: 'short',
      day: 'numeric'
    }).format(date).toUpperCase();
  }

  // ─── Helpers privés ───────────────────────────────────────────────────────

  private toGherkinLines(detail: ScenarioDetailDto | null): GherkinLine[] {
    if (!detail) return [];

    if (Array.isArray(detail.steps) && detail.steps.length > 0) {
      return detail.steps
        .sort((a, b) => (a.displayOrder ?? 0) - (b.displayOrder ?? 0))
        .map((step) => ({
          keyword: this.resolveStepKeyword(step.stepType),
          text: step.text
        }));
    }

    return (detail.gherkinContent || '')
      .split(/\r?\n/)
      .map((line) => line.trim())
      .filter(
        (line) =>
          !!line &&
          !line.startsWith('#') &&
          !/^feature\s*:/i.test(line) &&
          !/^scenario\s*:/i.test(line) &&
          !/^background\s*:/i.test(line)
      )
      .map((line) => {
        const match = line.match(/^(given|when|then|and|but)\s+(.*)$/i);
        if (!match) return null;
        return { keyword: match[1].toUpperCase(), text: match[2] } as GherkinLine;
      })
      .filter((line): line is GherkinLine => !!line);
  }

  private normalizeArray<T>(response: any): T[] {
    const payload = response?.resultat ?? response?.Resultat ?? response;
    return Array.isArray(payload) ? payload : [];
  }

  private mapScenarioStatus(status: number | string): 'draft' | 'active' | 'archived' {
    if (typeof status === 'string') {
      switch (status.toLowerCase()) {
        case 'active':
          return 'active';
        case 'archived':
        case 'deprecated':
          return 'archived';
        default:
          return 'draft';
      }
    }

    switch (status) {
      case 1: return 'active';
      case 2: return 'archived';
      default: return 'draft';
    }
  }

  private resolveStepKeyword(stepType: number | string | undefined): string {
    if (typeof stepType === 'string') {
      const normalized = stepType.trim().toUpperCase();
      return ProjectDetailComponent.STEP_KEYWORDS.includes(normalized) ? normalized : 'AND';
    }

    return ProjectDetailComponent.STEP_KEYWORDS[stepType ?? -1] ?? 'AND';
  }

  private toValidDate(dateValue: string | Date | undefined): Date | null {
    if (!dateValue) return null;
    const date = new Date(dateValue);
    if (isNaN(date.getTime()) || date.getFullYear() < 2000) return null;
    return date;
  }

  private getProjectFallbackDate(): Date | null {
    if (!this.project) return null;
    const raw =
      (this.project as any).lastRun ??
      (this.project as any).lastExecution ??
      (this.project as any).modifiedDate ??
      (this.project as any).createdDate;
    return this.toValidDate(raw);
  }

  highlightGherkinText(text: string): string {
    // Highlight parameters in angle brackets and quoted strings
    return text
      .replace(/<([^>]+)>/g, '<span class="text-primary font-semibold">&lt;$1&gt;</span>')
      .replace(/"([^"]+)"/g, '<span class="text-secondary font-semibold">"$1"</span>');
  }

  private normalizeHexColor(color: string | null | undefined): string {
    return /^#(?:[0-9a-f]{6})$/i.test(color ?? '') ? (color as string) : '#6366F1';
  }

  private dismissTagNoticeAfterDelay(): void {
    setTimeout(() => {
      this.tagNotice = null;
    }, 3500);
  }
}

interface ModuleDto {
  id: string;
  projectId: string;
  name: string;
  description?: string;
  displayOrder?: number;
}

interface FeatureDto {
  id: string;
  moduleId: string;
  name: string;
  description?: string;
  displayOrder?: number;
  scenarioCount?: number;
}

interface ScenarioListDto {
  id: string;
  featureId: string;
  featureName: string;
  title: string;
  description: string;
  status: number | string;
  currentVersion: number;
  createdAt: string | Date;
  updatedAt?: string | Date;
  stepCount?: number;
  lastTestStatus?: string;
}

interface ScenarioDetailDto {
  id: string;
  featureId: string;
  featureName: string;
  title: string;
  description: string;
  gherkinContent: string;
  status: number | string;
  currentVersion: number;
  createdAt: string | Date;
  updatedAt?: string | Date;
  steps?: StepDto[];
}

interface StepDto {
  id: string;
  stepType: number | string;
  text: string;
  displayOrder: number;
}

interface HierarchyModule {
  id: string;
  name: string;
  expanded: boolean;
  children: HierarchyFeature[];
}

interface HierarchyFeature {
  id: string;
  moduleId: string;
  name: string;
  expanded: boolean;
  children: HierarchyScenario[];
}

interface HierarchyScenario {
  id: string;
  name: string;
  status: 'draft' | 'active' | 'archived';
  lastTestStatus?: string;
}

interface GherkinLine {
  keyword: string;
  text: string;
}