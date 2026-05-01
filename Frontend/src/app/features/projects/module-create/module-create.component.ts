import { Component, OnInit, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { ModuleService } from '../../../core/services/module.service';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-module-create',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule],
  templateUrl: './module-create.component.html',
  styleUrl: './module-create.component.scss'
})
export class ModuleCreateComponent implements OnInit {
  /** When used as an inline modal, pass the projectId via input */
  @Input() modalProjectId: string = '';
  @Output() moduleCreated = new EventEmitter<void>();
  @Output() modalCancelled = new EventEmitter<void>();

  moduleForm: FormGroup;
  isSubmitting = false;
  errorMessage = '';
  projectId = '';
  currentRole = 'viewer';
  isRoleLoaded = false;

  get isViewerOrManager(): boolean {
    return this.currentRole === 'viewer' || this.currentRole === 'manager';
  }

  get f() {
    return this.moduleForm.controls;
  }

  constructor(
    private fb: FormBuilder,
    private moduleService: ModuleService,
    private route: ActivatedRoute,
    private router: Router,
    private authService: AuthService
  ) {
    this.moduleForm = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(100)]],
      description: ['', [Validators.required, Validators.maxLength(500)]],
      displayOrder: [0, [Validators.required, Validators.min(0)]]
    });
  }

  ngOnInit(): void {
    this.projectId = this.modalProjectId || this.route.snapshot.paramMap.get('projectId') || '';

    this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.currentRole = (user?.roles?.[0] || 'viewer').toLowerCase();
        this.isRoleLoaded = true;
      }
    });
  }

  canCreate(): boolean {
    return this.currentRole === 'admin' || this.currentRole === 'manager' || this.currentRole === 'tester';
  }

  onSubmit(): void {
    if (!this.canCreate()) {
      this.errorMessage = 'You do not have permission to create modules.';
      return;
    }

    if (this.moduleForm.invalid) {
      Object.keys(this.moduleForm.controls).forEach(key =>
        this.moduleForm.get(key)?.markAsTouched()
      );
      return;
    }

    if (!this.projectId) {
      this.errorMessage = 'Project ID is missing.';
      return;
    }

    this.isSubmitting = true;
    this.errorMessage = '';

    this.moduleService.createModule({
      projectId: this.projectId,
      name: this.moduleForm.value.name.trim(),
      description: this.moduleForm.value.description.trim(),
      displayOrder: this.moduleForm.value.displayOrder
    }).subscribe({
      next: () => {
        if (this.modalProjectId) {
          this.moduleCreated.emit();
        } else {
          this.router.navigate(['/projects', this.projectId]);
        }
      },
      error: (error) => {
        console.error('Erreur lors de la création du module:', error);
        this.errorMessage =
          error?.error?.fail_Messages ||
          error?.error?.Fail_Messages ||
          'Failed to create module. Please try again.';
        this.isSubmitting = false;
      }
    });
  }

  cancel(): void {
    if (this.modalProjectId) {
      this.modalCancelled.emit();
    } else {
      this.router.navigate(['/projects', this.projectId]);
    }
  }
}