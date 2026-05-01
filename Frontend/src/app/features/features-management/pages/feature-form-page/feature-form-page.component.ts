// src/app/features/features-management/pages/feature-form-page/feature-form-page.component.ts
import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, ActivatedRoute } from '@angular/router';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { FeatureService } from '../../../../core/services/feature.service';
import { AuthService } from '../../../../core/services/auth.service';
import { ModuleService } from '../../../../core/services/module.service';
import { ResponseHttp } from '../../../../core/models/response-http.model';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
@Component({
  selector: 'app-feature-form-page',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule],
  templateUrl: './feature-form-page.component.html',
  styleUrl: './feature-form-page.component.scss'
})
export class FeatureFormPageComponent implements OnInit, OnDestroy {
  featureForm: FormGroup;
  isEditMode = false;
  featureId: string = '';
  moduleId: string = '';
  moduleName: string = '';
  projectId: string = '';
  isLoading = false;
  isSubmitting = false;
  currentRole = 'viewer';
  errorMessage = '';
  modules: any[] = [];
  selectedModuleId = '';
  loadingModules = false;
  private destroy$ = new Subject<void>();
  constructor(
    private fb: FormBuilder,
    private featureService: FeatureService,
    private router: Router,
    private route: ActivatedRoute,
    private authService: AuthService,
    private moduleService: ModuleService
  ) {
    this.featureForm = this.fb.group({
      selectedModule: ['', Validators.required],
      name: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(100)]],
      description: ['', [Validators.required, Validators.maxLength(500)]],
      displayOrder: [0, [Validators.required, Validators.min(0)]]
    });
  }

  ngOnInit(): void {
    // Route params for edit mode (/features/:id/edit)
    this.route.params.pipe(takeUntil(this.destroy$)).subscribe(params => {
      this.featureId = params['id'] || '';
      this.isEditMode = !!this.featureId;
    });

    // Query params for create mode (/features/new?moduleId=...&moduleName=...&projectId=...)
    this.route.queryParams.pipe(takeUntil(this.destroy$)).subscribe(params => {
      this.moduleId = params['moduleId'] || '';
      this.moduleName = params['moduleName'] || 'Module';
      this.projectId = params['projectId'] || '';
    });

    this.authService.currentUser$.pipe(takeUntil(this.destroy$)).subscribe(user => {
      this.currentRole = (user?.roles?.[0] || 'viewer').toLowerCase();
    });

    if (this.isEditMode) {
      this.loadFeature();
    }
  }

  get f() {
    return this.featureForm.controls;
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  canSubmit(): boolean {
    return this.currentRole === 'admin' || this.currentRole === 'manager' || this.currentRole === 'tester';
  }
    // ================= MODULES =================

  loadModules(): void {
    if (!this.projectId) return;

    this.loadingModules = true;

    this.moduleService.getModulesByProjectId(this.projectId).subscribe({
      next: (res: ResponseHttp) => {
        const list = res?.resultat ?? [];

        this.modules = list.map((m: { id: string; name: string }) => ({
          id: m.id,
          name: m.name
        }));

        this.loadingModules = false;
      },
      error: () => {
        this.loadingModules = false;
      }
    });
  }

  // ================= LOAD FEATURE =================

  loadFeature(): void {
    this.isLoading = true;
    this.featureService.getFeatureById(this.featureId).subscribe({
      next: (response) => {
        if (response.status === 200 && response.resultat) {
          const feature = response.resultat;
          this.featureForm.patchValue({
            name: feature.name,
            description: feature.description,
            displayOrder: feature.displayOrder
          });
          this.moduleId = feature.moduleId;
          this.moduleName = feature.moduleName;
        }
        this.isLoading = false;
      },
      error: () => {
        this.errorMessage = 'Failed to load feature.';
        this.isLoading = false;
      }
    });
  }

  onSubmit(): void {
    if (!this.canSubmit()) {
      this.errorMessage = 'You do not have permission to create/edit features.';
      return;
    }

    if (this.featureForm.invalid) {
      Object.keys(this.featureForm.controls).forEach(key =>
        this.featureForm.get(key)?.markAsTouched()
      );
      return;
    }

    const finalModuleId = this.moduleId || this.featureForm.value.selectedModule;
    if (!this.isEditMode && !finalModuleId) {
      this.errorMessage = 'Module ID is missing. Go back and select a module.';
      return;
    }

    this.isSubmitting = true;
    this.errorMessage = '';
    const v = this.featureForm.value;

    if (this.isEditMode) {
      this.featureService.updateFeature(this.featureId, {
        name: v.name.trim(),
        description: v.description.trim(),
        displayOrder: v.displayOrder
      }).subscribe({
        next: () => this.router.navigate(['/features', this.featureId]),
        error: (err) => {
          this.errorMessage = err?.error?.fail_Messages || 'Failed to update feature.';
          this.isSubmitting = false;
        }
      });
    } else {
      this.featureService.createFeature({
        moduleId: finalModuleId,
        name: v.name.trim(),
        description: v.description.trim(),
        displayOrder: v.displayOrder
      }).subscribe({
        next: (response) => {
          const newId = response?.resultat?.id;
          if (newId) {
            this.router.navigate(['/features', newId]);
          } else {
            this.navigateBack();
          }
        },
        error: (err) => {
          this.errorMessage = err?.error?.fail_Messages || 'Failed to create feature.';
          this.isSubmitting = false;
        }
      });
    }
  }

  cancel(): void {
    if (this.isEditMode) {
      this.router.navigate(['/features', this.featureId]);
    } else {
      this.navigateBack();
    }
  }

  navigateBack(): void {
    this.router.navigate(['/features'], {
      queryParams: {
        moduleId: this.moduleId,
        moduleName: this.moduleName,
        projectId: this.projectId
      }
    });
  }
}