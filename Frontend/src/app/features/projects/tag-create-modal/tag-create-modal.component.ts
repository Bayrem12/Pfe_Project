import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '../../../core/services/auth.service';
import { TagService } from '../../../core/services/tag.service';
import { ProjectTag } from '../../../core/models/tag.model';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-tag-create-modal',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslatePipe],
  templateUrl: './tag-create-modal.component.html',
  styleUrl: './tag-create-modal.component.scss'
})
export class TagCreateModalComponent {
  @Input({ required: true }) projectId = '';
  @Output() tagCreated = new EventEmitter<ProjectTag>();
  @Output() modalClosed = new EventEmitter<void>();

  private fb = inject(FormBuilder);
  private tagService = inject(TagService);
  private authService = inject(AuthService);

  readonly colorPresets = [
    '#6366F1', '#8B5CF6', '#EC4899', '#F97316',
    '#EAB308', '#10B981', '#06B6D4', '#3B82F6'
  ];

  readonly tagForm = this.fb.group({
    name: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(50)]],
    color: ['#6366F1', [Validators.required, Validators.pattern(/^#(?:[0-9a-fA-F]{6})$/)]],
    description: ['', [Validators.maxLength(240)]]
  });

  isSubmitting = false;
  errorMessage = '';
  currentRole = 'viewer';

  constructor() {
    this.authService.currentUser$.subscribe(user => {
      this.currentRole = (user?.roles?.[0] || 'viewer').toLowerCase();
    });
  }

  get canCreate(): boolean {
    return this.currentRole === 'admin' || this.currentRole === 'manager' || this.currentRole === 'tester';
  }

  get controls() {
    return this.tagForm.controls;
  }

  selectColor(color: string): void {
    this.tagForm.patchValue({ color });
    this.tagForm.get('color')?.markAsDirty();
  }

  close(): void {
    if (!this.isSubmitting) {
      this.modalClosed.emit();
    }
  }

  submit(): void {
    if (!this.canCreate) {
      this.errorMessage = 'You do not have permission to create tags.';
      return;
    }

    if (this.tagForm.invalid) {
      this.tagForm.markAllAsTouched();
      return;
    }

    if (!this.projectId) {
      this.errorMessage = 'Project context is missing.';
      return;
    }

    this.isSubmitting = true;
    this.errorMessage = '';

    this.tagService.createTag({
      projectId: this.projectId,
      name: this.controls.name.value!.trim(),
      color: this.controls.color.value || '#6366F1',
      description: this.controls.description.value?.trim() || null
    }).subscribe({
      next: (response) => {
        this.isSubmitting = false;
        this.tagCreated.emit(response.resultat ?? null);
      },
      error: (error) => {
        this.isSubmitting = false;
        this.errorMessage =
          error?.error?.fail_Messages ||
          error?.error?.Fail_Messages ||
          'Unable to create the tag right now. Please try again.';
      }
    });
  }
}
