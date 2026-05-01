import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs/operators';

import { AuthService } from '../../../core/services/auth.service';
import { FeatureService } from '../../../core/services/feature.service';
import { ModuleService } from '../../../core/services/module.service';
import { Module } from '../../../core/models/module.model';

export interface CreatedFeaturePayload {
  id: string;
  moduleId: string;
  name: string;
}

@Component({
  selector: 'app-feature-create-modal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './feature-create-modal.component.html'
})
export class FeatureCreateModalComponent implements OnInit {
  @Input() projectId = '';
  @Input() moduleId = '';
  @Input() moduleName = '';

  @Output() featureCreated = new EventEmitter<CreatedFeaturePayload>();
  @Output() modalClosed = new EventEmitter<void>();

  featureForm: FormGroup;

  modules: Module[] = [];
  loadingModules = false;
  isSubmitting = false;
  errorMessage = '';

  currentRole = 'viewer';
  isRoleLoaded = false;

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private featureService: FeatureService,
    private moduleService: ModuleService
  ) {
    this.featureForm = this.fb.group({
      selectedModule: ['', Validators.required],
      name: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(100)]],
      description: ['', [Validators.required, Validators.maxLength(500)]],
      displayOrder: [0, [Validators.required, Validators.min(0)]]
    });
  }

  get f() {
    return this.featureForm.controls;
  }

  ngOnInit(): void {
    this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.currentRole = (user?.roles?.[0] || 'viewer').toLowerCase();
      }
      this.isRoleLoaded = true;
    });

    if (this.moduleId) {
      this.featureForm.patchValue({ selectedModule: this.moduleId });
    }

    if (this.projectId) {
      this.loadModules();
    }
  }

  canCreate(): boolean {
    return this.currentRole === 'admin' || this.currentRole === 'manager' || this.currentRole === 'tester';
  }

  close(): void {
    if (this.isSubmitting) {
      return;
    }

    this.modalClosed.emit();
  }

  onSubmit(): void {
    if (!this.canCreate()) {
      this.errorMessage = 'You do not have permission to create features.';
      return;
    }

    if (this.featureForm.invalid) {
      Object.keys(this.featureForm.controls).forEach(key => {
        this.featureForm.get(key)?.markAsTouched();
      });
      return;
    }

    const selectedModuleId = this.featureForm.value.selectedModule as string;
    if (!selectedModuleId) {
      this.errorMessage = 'Please select a module first.';
      return;
    }

    this.isSubmitting = true;
    this.errorMessage = '';

    this.featureService.createFeature({
      moduleId: selectedModuleId,
      name: (this.featureForm.value.name || '').trim(),
      description: (this.featureForm.value.description || '').trim(),
      displayOrder: this.featureForm.value.displayOrder ?? 0
    })
      .pipe(finalize(() => {
        this.isSubmitting = false;
      }))
      .subscribe({
        next: (response: any) => {
          const created = response?.resultat ?? response?.Resultat ?? null;
          this.featureCreated.emit({
            id: created?.id ?? '',
            moduleId: selectedModuleId,
            name: created?.name ?? this.featureForm.value.name
          });
          this.modalClosed.emit();
        },
        error: (error) => {
          this.errorMessage =
            error?.error?.fail_Messages ||
            error?.error?.Fail_Messages ||
            'Failed to create feature. Please try again.';
        }
      });
  }

  private loadModules(): void {
    this.loadingModules = true;

    this.moduleService.getModulesByProjectId(this.projectId).subscribe({
      next: (res: any) => {
        const data = res?.resultat ?? res;
        this.modules = Array.isArray(data) ? data : [];

        const selected = this.featureForm.value.selectedModule as string;
        const hasSelected = !!selected && this.modules.some(m => m.id === selected);

        if (!hasSelected) {
          if (this.moduleId && this.modules.some(m => m.id === this.moduleId)) {
            this.featureForm.patchValue({ selectedModule: this.moduleId });
          } else if (this.modules.length) {
            this.featureForm.patchValue({ selectedModule: this.modules[0].id });
          }
        }

        this.loadingModules = false;
      },
      error: () => {
        this.modules = [];
        this.loadingModules = false;
      }
    });
  }
}
