import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';

import { ScenarioService } from '../../../../core/services/scenario.service';
import { FeatureService } from '../../../../core/services/feature.service';
import { ProjectService } from '../../../../core/services/project.service';
import { ModuleService } from '../../../../core/services/module.service';
import { AuthService } from '../../../../core/services/auth.service';
import { BreadcrumbService } from '../../../../core/services/breadcrumb.service';
import { TagService } from '../../../../core/services/tag.service';
import { StepType } from '../../../../core/models/scenario.model';
import { ProjectTag } from '../../../../core/models/tag.model';
import { ResponseHttp } from '../../../../core/models/response-http.model';
import { ModuleCreateComponent } from '../../../projects/module-create/module-create.component';
import { CreatedFeaturePayload, FeatureCreateModalComponent } from '../../../projects/feature-create-modal/feature-create-modal.component';
import { TagCreateModalComponent } from '../../../projects/tag-create-modal/tag-create-modal.component';
import { TranslatePipe } from '../../../../shared/pipes/translate.pipe';
import { TranslationService } from '../../../../core/services/translation.service';

interface DropdownItem {
  id: string;
  name: string;
}

interface ScenarioStep {
  keyword: StepType;
  text: string;
}

interface DataTableRow {
  [key: string]: string;
}

@Component({
  selector: 'app-scenario-editor-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, ModuleCreateComponent, FeatureCreateModalComponent, TagCreateModalComponent, TranslatePipe],
  templateUrl: './scenario-editor-page.component.html'
})
export class ScenarioEditorPageComponent implements OnInit, OnDestroy {

  scenarioForm: FormGroup;

  scenarioId = '';
  isEditMode = false;

  projectId = '';
  projectName = '';
  moduleId = '';
  moduleName = '';
  featureId = '';
  featureName = '';

  projects: DropdownItem[] = [];
  modules: DropdownItem[] = [];
  features: DropdownItem[] = [];

  loadingProjects = false;
  loadingModules = false;
  loadingFeatures = false;

  showFeatureSelection = false;
  showCreateModuleModal = false;
  showCreateFeatureModal = false;
  showCreateTagModal = false;

  // Steps
  steps: ScenarioStep[] = [
    { keyword: StepType.Given, text: '' },
    { keyword: StepType.When, text: '' },
    { keyword: StepType.Then, text: '' }
  ];

  stepKeywords = [
    { value: StepType.Given, label: 'GIVEN', color: 'bg-blue-50 text-blue-700' },
    { value: StepType.When, label: 'WHEN', color: 'bg-purple-50 text-purple-700' },
    { value: StepType.Then, label: 'THEN', color: 'bg-green-50 text-green-700' },
    { value: StepType.And, label: 'AND', color: 'bg-slate-50 text-slate-700' },
    { value: StepType.But, label: 'BUT', color: 'bg-orange-50 text-orange-700' }
  ];

  // Data Table
  dataTableColumns: string[] = [];
  dataTableRows: DataTableRow[] = [];
  showDataTable = false;

  // Tags
  tags: string[] = [];
  newTagInput = '';
  projectTags: ProjectTag[] = [];
  loadingProjectTags = false;

  // Status
  activeStatus = true; // true = Active, false = Draft

  // Validation
  isGherkinValid = false;
  validationErrors: string[] = [];
  validationWarnings: string[] = [];
  autoSaving = false;

  loading = false;
  isSubmitting = false;

  private destroy$ = new Subject<void>();

  get canCreateScenario(): boolean {
    return this.authService.canCreateScenarios();
  }

  get isViewerRole(): boolean {
    return this.authService.isProjectReadOnly();
  }

  private t(key: string, ...args: string[]): string {
    return this.translationService.t(key, ...args);
  }

  private normalizeTag(tag: string): string {
    return (tag || '').replace(/^@/, '').trim();
  }

  private dedupeTags(tags: string[]): string[] {
    const seen = new Set<string>();
    const result: string[] = [];

    for (const rawTag of tags) {
      const normalized = this.normalizeTag(rawTag);
      const key = normalized.toLowerCase();

      if (!normalized || seen.has(key)) {
        continue;
      }

      seen.add(key);
      result.push(normalized);
    }

    return result;
  }

  private dedupeProjectTags(tags: ProjectTag[]): ProjectTag[] {
    const seen = new Set<string>();

    return [...tags]
      .filter(tag => {
        const key = this.normalizeTag(tag.name).toLowerCase();
        if (!key || seen.has(key)) {
          return false;
        }

        seen.add(key);
        return true;
      })
      .sort((a, b) => a.name.localeCompare(b.name));
  }

  private hasTag(tagName: string): boolean {
    const normalized = this.normalizeTag(tagName).toLowerCase();
    return this.tags.some(tag => tag.toLowerCase() === normalized);
  }

  private mapServerScenarioError(message: unknown, fallbackKey: string): string {
    const text = String(message ?? '').trim();
    return text || this.t(fallbackKey);
  }

  constructor(
    private fb: FormBuilder,
    private scenarioService: ScenarioService,
    private featureService: FeatureService,
    private projectService: ProjectService,
    private moduleService: ModuleService,
    private authService: AuthService,
    private breadcrumbService: BreadcrumbService,
    private tagService: TagService,
    private translationService: TranslationService,
    private router: Router,
    private route: ActivatedRoute
  ) {
    this.scenarioForm = this.fb.group({
      title: ['', [Validators.required, Validators.minLength(3)]],
      description: ['', [Validators.required]]
    });
  }

  ngOnInit(): void {
    this.route.params.pipe(takeUntil(this.destroy$)).subscribe(p => {
      this.scenarioId = p['id'] || '';
      this.isEditMode = !!this.scenarioId;

      if (this.isEditMode) {
        this.loadScenario();
      } else {
        this.updateBreadcrumb();
      }
    });

    this.route.queryParams.pipe(takeUntil(this.destroy$)).subscribe(p => {
      this.featureId = p['featureId'] || '';
      this.featureName = p['featureName'] || '';
      this.projectId = p['projectId'] || '';
      this.projectName = p['projectName'] || '';

      if (this.projectId && !this.isEditMode) {
        this.loadModules(this.projectId);
        this.loadProjectTagsForEditor(this.projectId);
        this.updateBreadcrumb();
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.breadcrumbService.clear();
  }

  // ===================== PROJECTS =====================

  loadProjects(userId: string): void {
    this.loadingProjects = true;
    this.projectService.getProjects(userId).subscribe({
      next: (res: ResponseHttp) => {
        const data = res?.resultat;
        const list = Array.isArray(data)
          ? data
          : (data?.items ?? data?.Items ?? []);

        this.projects = list.map((p: { id: string; name: string }) => ({
          id: p.id,
          name: p.name
        }));
        this.loadingProjects = false;
      },
      error: () => {
        this.projects = [];
        this.loadingProjects = false;
      }
    });
  }

  onProjectChange(id: string): void {
    this.projectId = id;
    this.projectName = this.projects.find(p => p.id === id)?.name || '';
    this.moduleId = '';
    this.moduleName = '';
    this.featureId = '';
    this.featureName = '';
    this.modules = [];
    this.features = [];
    if (id) this.loadModules(id);
  }

  // ===================== MODULES =====================

  loadModules(projectId: string): void {
    this.loadingModules = true;
    this.moduleService.getModulesByProjectId(projectId).subscribe({
      next: (res: ResponseHttp) => {
        const list = res?.resultat ?? [];
        this.modules = (Array.isArray(list) ? list : []).map((m: { id: string; name: string }) => ({
          id: m.id,
          name: m.name
        }));
        this.loadingModules = false;
      },
      error: () => {
        this.modules = [];
        this.loadingModules = false;
      }
    });
  }

  onModuleChange(id: string): void {
    this.moduleId = id;
    this.moduleName = this.modules.find(m => m.id === id)?.name || '';
    this.featureId = '';
    this.featureName = '';
    this.features = [];
    if (id) this.loadFeatures(id);
  }

  // ===================== FEATURES =====================

  loadFeatures(moduleId: string): void {
    this.loadingFeatures = true;
    this.featureService.getFeaturesByModule(moduleId).subscribe({
      next: (res: ResponseHttp) => {
        const list = res?.resultat ?? [];
        this.features = (Array.isArray(list) ? list : []).map((f: { id: string; name: string }) => ({
          id: f.id,
          name: f.name
        }));
        this.loadingFeatures = false;
      },
      error: () => {
        this.features = [];
        this.loadingFeatures = false;
      }
    });
  }

  onFeatureChange(id: string): void {
    this.featureId = id;
    this.featureName = this.features.find(f => f.id === id)?.name || '';
  }

  openCreateModuleModal(): void {
    this.validationErrors = [];

    if (!this.projectId) {
      this.validationErrors = [this.t('scenario.editor.error.projectContextMissing')];
      return;
    }

    this.showCreateModuleModal = true;
  }

  onInlineModuleCreated(): void {
    this.showCreateModuleModal = false;
    this.loadModules(this.projectId);
  }

  onInlineModuleCreateCancelled(): void {
    this.showCreateModuleModal = false;
  }

  openCreateFeatureModal(): void {
    this.validationErrors = [];

    if (!this.projectId) {
      this.validationErrors = [this.t('scenario.editor.error.projectContextMissing')];
      return;
    }

    this.showCreateFeatureModal = true;
  }

  onInlineFeatureCreated(payload: CreatedFeaturePayload): void {
    this.showCreateFeatureModal = false;

    if (!payload.moduleId) {
      return;
    }

    if (this.moduleId !== payload.moduleId) {
      this.moduleId = payload.moduleId;
      this.moduleName = this.modules.find(m => m.id === payload.moduleId)?.name || this.moduleName;
    }

    this.loadFeatures(payload.moduleId);

    this.featureId = payload.id;
    this.featureName = payload.name;
  }

  onInlineFeatureCreateCancelled(): void {
    this.showCreateFeatureModal = false;
  }

  openCreateTagModal(): void {
    if (!this.projectId) {
      this.validationErrors = [this.t('scenario.editor.error.projectContextMissing')];
      return;
    }
    this.showCreateTagModal = true;
  }

  onTagCreated(tag: ProjectTag | null): void {
    this.showCreateTagModal = false;
    if (!tag) return;
    this.projectTags = this.dedupeProjectTags([...this.projectTags, tag]);
    this.tags = this.dedupeTags([...this.tags, tag.name]);
  }

  onTagCreateCancelled(): void {
    this.showCreateTagModal = false;
  }

  // ===================== STEPS =====================

  addStep(): void {
    const lastKeyword = this.steps.length > 0 ? StepType.And : StepType.Given;
    this.steps.push({ keyword: lastKeyword, text: '' });
  }

  removeStep(index: number): void {
    if (this.steps.length > 1) {
      this.steps.splice(index, 1);
    }
  }

  onStepKeyPress(event: KeyboardEvent, index: number): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.addStep();
      setTimeout(() => {
        const inputs = document.querySelectorAll<HTMLInputElement>('.step-text-input');
        inputs[inputs.length - 1]?.focus();
      }, 50);
    }
  }

  getKeywordColor(keyword: StepType): string {
    return this.stepKeywords.find(k => k.value === keyword)?.color || 'bg-slate-50 text-slate-700';
  }

  getPreviewKeywordClass(keyword: StepType): string {
    const map: Record<string, string> = {
      [StepType.Given]: 'text-blue-400',
      [StepType.When]: 'text-purple-400',
      [StepType.Then]: 'text-green-400',
      [StepType.And]: 'text-yellow-400',
      [StepType.But]: 'text-orange-400'
    };
    return map[keyword] || 'text-slate-300';
  }

  // ===================== DATA TABLE =====================

  toggleDataTable(): void {
    this.showDataTable = !this.showDataTable;
    if (this.showDataTable && this.dataTableColumns.length === 0) {
      this.dataTableColumns = ['Column 1', 'Column 2'];
      this.dataTableRows = [{ 'Column 1': '', 'Column 2': '' }];
    }
  }

  addDataTableRow(): void {
    const row: DataTableRow = {};
    this.dataTableColumns.forEach(col => row[col] = '');
    this.dataTableRows.push(row);
  }

  addDataTableColumn(): void {
    const newCol = `Column ${this.dataTableColumns.length + 1}`;
    this.dataTableColumns.push(newCol);
    this.dataTableRows.forEach(row => row[newCol] = '');
  }

  removeDataTableRow(index: number): void {
    this.dataTableRows.splice(index, 1);
  }

  // ===================== PROJECT TAGS =====================

  loadProjectTagsForEditor(projectId: string): void {
    this.loadingProjectTags = true;
    this.tagService.getProjectTags(projectId).subscribe({
      next: (res) => {
        this.projectTags = this.dedupeProjectTags(res?.resultat ?? []);
        this.loadingProjectTags = false;
      },
      error: () => { this.loadingProjectTags = false; }
    });
  }

  isProjectTagSelected(tagName: string): boolean {
    return this.hasTag(tagName);
  }

  isInProjectTags(tagName: string): boolean {
    const normalized = tagName.replace(/^@/, '').toLowerCase();
    return this.projectTags.some(pt => pt.name.replace(/^@/, '').toLowerCase() === normalized);
  }

  toggleProjectTag(tagName: string): void {
    const normalized = this.normalizeTag(tagName);
    if (!this.hasTag(normalized)) {
      this.tags = this.dedupeTags([...this.tags, normalized]);
    } else {
      this.tags = this.tags.filter(tag => tag.toLowerCase() !== normalized.toLowerCase());
    }
  }

  // ===================== TAGS =====================

  addTag(): void {
    const tag = this.normalizeTag(this.newTagInput);
    if (tag && !this.hasTag(tag)) {
      this.tags = this.dedupeTags([...this.tags, tag]);
      this.newTagInput = '';
    }
  }

  removeTag(index: number): void {
    this.tags.splice(index, 1);
  }

  onTagKeyPress(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.addTag();
    }
  }

  // ===================== GHERKIN PREVIEW =====================

  get liveGherkinPreview(): string {
    const lines: string[] = [];

    if (this.tags.length > 0) {
      lines.push(this.tags.map(t => `@${t}`).join(' '));
    }

    lines.push(`Feature: ${this.featureName || 'Untitled Feature'}`);
    lines.push('');
    lines.push(`  Scenario: ${this.scenarioForm.value.title || 'Untitled Scenario'}`);

    for (const step of this.steps) {
      if (step.text) {
        lines.push(`    ${step.keyword} ${step.text}`);
      }
    }

    if (this.showDataTable && this.dataTableColumns.length > 0 && this.dataTableRows.length > 0) {
      lines.push('');
      lines.push('    Examples:');
      lines.push(`      | ${this.dataTableColumns.join(' | ')} |`);
      for (const row of this.dataTableRows) {
        const vals = this.dataTableColumns.map(col => row[col] || '');
        lines.push(`      | ${vals.join(' | ')} |`);
      }
    }

    return lines.join('\n');
  }

  copyGherkin(): void {
    navigator.clipboard.writeText(this.liveGherkinPreview);
  }

  // ===================== VALIDATION =====================

  validateGherkin(): void {
    const content = this.liveGherkinPreview;
    this.scenarioService.validateGherkin(content).subscribe({
      next: (res: ResponseHttp) => {
        const result = res?.resultat;
        this.isGherkinValid = result?.isValid ?? false;
        this.validationErrors = result?.errors || [];
        this.validationWarnings = result?.warnings || [];
      }
    });
  }

  // ===================== SCENARIO LOAD =====================

  loadScenario(): void {
    this.loading = true;
    this.scenarioService.getScenarioById(this.scenarioId).subscribe({
      next: (res: ResponseHttp) => {
        const s = res.resultat;
        this.featureId = s.featureId;
        this.featureName = s.featureName;

        // Set module info from backend
        if (s.moduleId) {
          this.moduleId = s.moduleId;
          this.moduleName = s.moduleName || '';
          // Populate modules dropdown with current module
          this.modules = [{ id: s.moduleId, name: s.moduleName || '' }];
          // Load features for this module so the dropdown works
          this.loadFeatures(s.moduleId);
        }

        // Set project info from backend
        if (s.projectId) {
          this.projectId = s.projectId;
          this.projectName = s.projectName || '';
          this.loadProjectTagsForEditor(s.projectId);
        }

        this.scenarioForm.patchValue({
          title: s.title,
          description: s.description
        });

        this.activeStatus = s.status === 'Active' || s.status === 1;

        // Parse tags (strip @ prefix used in Gherkin – stored without @ in UI)
        if (s.tags && s.tags.length > 0) {
          this.tags = this.dedupeTags(s.tags.map((t: string) => this.normalizeTag(t)));
        }

        // Parse steps from backend
        if (s.steps && s.steps.length > 0) {
          const stepTypeMap: Record<number, StepType> = {
            0: StepType.Given, 1: StepType.When, 2: StepType.Then,
            3: StepType.And, 4: StepType.But
          };
          this.steps = s.steps
            .sort((a: { displayOrder: number }, b: { displayOrder: number }) => a.displayOrder - b.displayOrder)
            .map((step: { stepType: number; text: string }) => ({
              keyword: stepTypeMap[step.stepType] ?? step.stepType as unknown as StepType,
              text: step.text
            }));
        }

        this.loading = false;
        this.updateBreadcrumb();
      },
      error: () => { this.loading = false; }
    });
  }

  updateBreadcrumb(): void {
    const qp = this.projectId
      ? { projectId: this.projectId, projectName: this.projectName }
      : undefined;
    const crumbs: { label: string; url?: string; queryParams?: { [key: string]: string } }[] = [];
    if (this.projectName) {
      crumbs.push({ label: this.projectName, url: '/scenarios', queryParams: qp });
    }
    crumbs.push({ label: 'Scenarios', url: '/scenarios', queryParams: qp });
    if (this.isEditMode) {
      crumbs.push({ label: this.scenarioForm.value.title || 'Scenario', url: `/scenarios/${this.scenarioId}` });
      crumbs.push({ label: 'Edit' });
    } else {
      crumbs.push({ label: 'New Scenario' });
    }
    this.breadcrumbService.setMultiple(crumbs);
  }

  // ===================== CRUD =====================

  onSubmit(asDraft: boolean = false): void {
    if (!this.isEditMode && !this.canCreateScenario) {
      this.validationErrors = [this.t('scenario.editor.error.createRole')];
      return;
    }

    if (this.isEditMode && this.isViewerRole) {
      this.validationErrors = [this.t('scenario.editor.error.updateReadonly')];
      return;
    }

    if (!this.featureId && !this.isEditMode) {
      this.validationErrors = [this.t('scenario.editor.error.featureRequired')];
      return;
    }

    if (!this.scenarioForm.valid) {
      if (this.scenarioForm.get('title')?.hasError('minlength')) {
        this.validationErrors = [this.t('scenario.editor.titleMin')];
      } else {
        this.validationErrors = [this.t('scenario.editor.error.titleRequired')];
      }
      return;
    }

    // Validate description
    const descriptionValue = (this.scenarioForm.value.description || '').trim();
    if (!descriptionValue) {
      this.validationErrors = [this.t('scenario.editor.error.descriptionRequired')];
      return;
    }

    // Validate steps
    if (this.steps.length === 0) {
      this.validationErrors = [this.t('scenario.editor.error.stepsRequired')];
      return;
    }

    const emptySteps = this.steps
      .map((s, i) => ({ ...s, index: i + 1 }))
      .filter(s => !s.text || !s.text.trim());

    if (emptySteps.length > 0) {
      this.validationErrors = emptySteps.map(s => this.t('scenario.editor.error.stepEmpty', String(s.index), s.keyword));
      return;
    }

    this.isSubmitting = true;
    const gherkinContent = this.liveGherkinPreview;

    const request = {
      featureId: this.featureId,
      title: this.scenarioForm.value.title,
      description: this.scenarioForm.value.description || '',
      gherkinContent,
      tags: this.tags,
      status: this.activeStatus ? 'Active' : 'Draft'
    };

    if (this.isEditMode) {
      this.scenarioService.updateScenario(this.scenarioId, {
        ...request,
        changeDescription: `Updated scenario`
      }).subscribe({
        next: () => {
          this.isSubmitting = false;
          this.router.navigate(['/scenarios', this.scenarioId]);
        },
        error: (err) => {
          this.isSubmitting = false;
          this.validationErrors = [this.mapServerScenarioError(err?.error?.fail_Messages || err?.message, 'scenario.editor.error.updateFailed')];
        }
      });
    } else {
      this.scenarioService.createScenario(request).subscribe({
        next: (res) => {
          this.isSubmitting = false;
          const id = res?.resultat?.id;
          this.router.navigate(['/scenarios', id]);
        },
        error: (err) => {
          this.isSubmitting = false;
          this.validationErrors = [this.mapServerScenarioError(err?.error?.fail_Messages || err?.message, 'scenario.editor.error.createFailed')];
        }
      });
    }
  }

  cancel(): void {
    if (this.isEditMode) {
      this.router.navigate(['/scenarios', this.scenarioId]);
    } else {
      this.router.navigate(['/scenarios'], {
        queryParams: { projectId: this.projectId, projectName: this.projectName }
      });
    }
  }
}