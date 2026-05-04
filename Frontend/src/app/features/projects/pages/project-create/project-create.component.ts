import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, AbstractControl, ValidationErrors } from '@angular/forms';
import { Router } from '@angular/router';
import { CreateProjectRequest, Project, UpdateProjectRequest } from '../../../../core/models/project.model';
import { ResponseHttp } from '../../../../core/models/response-http.model';
import { ProjectService } from '../../../../core/services/project.service';
import { AuthService } from '../../../../core/services/auth.service';
import { TranslatePipe } from '../../../../shared/pipes/translate.pipe';
import { TranslationService } from '../../../../core/services/translation.service';

function urlFormatValidator(ctrl: AbstractControl): ValidationErrors | null {
  const v: string = ctrl.value || '';
  if (!v) return null;
  const valid = v.startsWith('http://') || v.startsWith('https://');
  return valid ? null : { invalidUrl: true };
}

type CreateProjectResponse = ResponseHttp<Project & { Id?: string }> & {
  Resultat?: Project & { Id?: string };
};

@Component({
  selector: 'app-project-create',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, TranslatePipe],
  templateUrl: './project-create.component.html',
  styleUrl: './project-create.component.scss'
})
export class ProjectCreateComponent {
    get isViewerOrManager(): boolean {
      return this.currentRole === 'viewer' || this.currentRole === 'manager';
    }
  projectForm: FormGroup;
  isSubmitting = false;
  createProjectError = '';
  formSubmitAttempted = false;

  userId = '';
  currentRole = 'viewer';

  constructor(
    private fb: FormBuilder,
    private projectService: ProjectService,
    private router: Router,
    private authService: AuthService,
    private translationService: TranslationService
  ) {
    this.projectForm = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(4), Validators.maxLength(200)]],
      description: ['', [Validators.required, Validators.maxLength(1000)]],
      url: ['', [urlFormatValidator, Validators.maxLength(500)]],
      isActive: [true]
    });

    this.authService.currentUser$.subscribe(user => {
      this.userId = user?.id || this.userId;
      this.currentRole = (user?.roles?.[0] || 'viewer').toLowerCase();
    });
  }

  get f() {
    return this.projectForm.controls;
  }

  private mapCreateProjectError(message: unknown): string {
    const text = String(message ?? '').trim();
    const normalized = text.toLowerCase();

    if (!normalized) {
      return this.translationService.t('project.create.error.generic');
    }

    const looksDuplicate =
      normalized.includes('already exists') ||
      normalized.includes('already exist') ||
      normalized.includes('existe déjà') ||
      normalized.includes('existe deja');

    if (looksDuplicate && normalized.includes('url')) {
      return this.translationService.t('project.create.error.duplicateUrl');
    }

    if (
      looksDuplicate ||
      normalized.includes('choose another name') ||
      normalized.includes('choisir un autre nom')
    ) {
      return this.translationService.t('project.create.error.duplicateName');
    }

    return text;
  }

  onSubmit(): void {
    if (!(this.currentRole === 'admin' || this.currentRole === 'manager')) {
      return;
    }

    if (this.projectForm.invalid) {
      this.projectForm.markAllAsTouched();
      this.projectForm.updateValueAndValidity();
      this.formSubmitAttempted = true;
      return;
    }
    this.formSubmitAttempted = false;

    this.isSubmitting = true;
    this.createProjectError = '';

    const projectRequest: CreateProjectRequest = {
      name: this.projectForm.value.name,
      description: this.projectForm.value.description,
      url: this.projectForm.value.url || '',
      userId: this.userId
    };

    const projectData = {
      Name: projectRequest.name,
      Description: projectRequest.description,
      Url: projectRequest.url,
      UserId: projectRequest.userId,
      IsActive: !!this.projectForm.value.isActive
    };

    console.log('Envoi des données:', projectData);

    this.projectService.createProject(projectData as unknown as CreateProjectRequest).subscribe({
      next: (response: CreateProjectResponse) => {
        console.log('Réponse création:', response);

        // Le controller retourne Ok() même pour les erreurs métier (nom/URL déjà existant)
        const errorMsg = (response as any)?.fail_Messages || (response as any)?.Fail_Messages;
        if (errorMsg) {
          this.createProjectError = this.mapCreateProjectError(errorMsg);
          this.isSubmitting = false;
          return;
        }

        const shouldBeActive = !!this.projectForm.value.isActive;
        const createdId = response?.resultat?.id || response?.resultat?.Id || response?.Resultat?.id || response?.Resultat?.Id;

        // Safety net: if creation endpoint ignores IsActive, force status after creation
        if (!shouldBeActive && createdId) {
          const updatePayload: UpdateProjectRequest = {
            projectId: createdId,
            name: this.projectForm.value.name,
            description: this.projectForm.value.description,
            url: this.projectForm.value.url || '',
            isActive: false
          };

          this.projectService.updateProject(createdId, updatePayload).subscribe({
            next: () => this.router.navigate(['/projects']),
            error: () => this.router.navigate(['/projects'])
          });
          return;
        }

        this.router.navigate(['/projects']);
      },
      error: (error) => {
        console.error('Erreur lors de la création:', error);
        this.createProjectError = this.mapCreateProjectError(
          error?.error?.fail_Messages ||
          error?.error?.Fail_Messages ||
          error?.message ||
          this.translationService.t('project.create.error.generic')
        );
        this.isSubmitting = false;
      }
    });
  }

  cancel(): void {
    this.router.navigate(['/projects']);
  }
}
