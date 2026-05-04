import { Component, OnInit, inject, HostListener, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';
import { Router } from '@angular/router';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { TestSuiteService } from '../services/test-suite.service';
import { AuthService } from '../../../core/services/auth.service';
import { Project } from '../../../core/models/project.model';
import { ProjectService } from '../../../core/services/project.service';
import { TestSuiteDto } from '../models/test-suite.model';
import { ConfirmDeleteDialogComponent } from '../../../shared/components/confirm-delete-dialog/confirm-delete-dialog.component';

@Component({
  selector: 'app-test-suites-list-page',
  standalone: true,
  imports: [TranslatePipe, CommonModule, ReactiveFormsModule, ConfirmDeleteDialogComponent],
  templateUrl: './test-suites-list-page.component.html'
})
export class TestSuitesListPageComponent implements OnInit {
  private testSuiteService = inject(TestSuiteService);
  private projectService = inject(ProjectService);
  private authService = inject(AuthService);
  private router = inject(Router);
  private fb = inject(FormBuilder);
  private elementRef = inject(ElementRef);

  projects: Project[] = [];
  suites: TestSuiteDto[] = [];
  filteredSuites: TestSuiteDto[] = [];
  selectedProjectId = '';
  searchTerm = '';
  viewMode: 'grid' | 'list' = 'grid';

  loading = false;
  creating = false;
  deleting = false;
  errorMessage = '';
  createError = '';
  projectDropdownOpen = false;

  showCreateModal = false;
  suiteToDelete: TestSuiteDto | null = null;
  suiteToEdit: TestSuiteDto | null = null;
  editing = false;

  createForm: FormGroup = this.fb.group({
    name: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(200)]],
    description: ['', [Validators.maxLength(1000)]]
  });

  editForm: FormGroup = this.fb.group({
    name: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(200)]],
    description: ['', [Validators.maxLength(1000)]]
  });

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.elementRef.nativeElement.contains(event.target)) {
      this.projectDropdownOpen = false;
    }
  }

  ngOnInit(): void {
    this.loadProjects();
  }

  get isViewerRole(): boolean {
    return this.authService.isProjectReadOnly();
  }

  openCreateModal(): void {
    if (this.isViewerRole) {
      this.errorMessage = 'Viewer role is read-only and cannot create test suites.';
      return;
    }

    this.showCreateModal = true;
  }

  loadProjects(): void {
    this.projectService.getUserProjects().subscribe({
      next: (projects) => this.projects = projects,
      error: () => this.errorMessage = 'Failed to load projects.'
    });
  }

  onProjectChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    this.selectedProjectId = select.value;
    this.searchTerm = '';
    if (this.selectedProjectId) {
      this.loadSuites();
    } else {
      this.suites = [];
      this.filteredSuites = [];
    }
  }

  selectProject(projectId: string): void {
    this.selectedProjectId = projectId;
    this.projectDropdownOpen = false;
    this.searchTerm = '';
    if (projectId) {
      this.loadSuites();
    } else {
      this.suites = [];
      this.filteredSuites = [];
    }
  }

  getSelectedProjectName(): string {
    const project = this.projects.find(p => p.id === this.selectedProjectId);
    return project ? project.name : '';
  }

  loadSuites(): void {
    this.loading = true;
    this.errorMessage = '';
    this.testSuiteService.getTestSuitesByProject(this.selectedProjectId).subscribe({
      next: (suites) => {
        this.suites = suites;
        this.applyFilter();
        this.loading = false;
      },
      error: () => {
        this.errorMessage = 'Failed to load test suites.';
        this.loading = false;
      }
    });
  }

  onSearchChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.searchTerm = input.value;
    this.applyFilter();
  }

  applyFilter(): void {
    if (!this.searchTerm) {
      this.filteredSuites = [...this.suites];
    } else {
      const term = this.searchTerm.toLowerCase();
      this.filteredSuites = this.suites.filter(s =>
        s.name.toLowerCase().includes(term) ||
        s.description?.toLowerCase().includes(term)
      );
    }
  }

  clearSearch(): void {
    this.searchTerm = '';
    this.applyFilter();
  }

  createSuite(): void {
    if (this.isViewerRole) {
      this.errorMessage = 'Viewer role is read-only and cannot create test suites.';
      return;
    }

    if (this.createForm.invalid || !this.selectedProjectId) return;

    this.creating = true;
    this.createError = '';
    const userId = this.authService.getCurrentUserId();
    this.testSuiteService.createTestSuite({
      projectId: this.selectedProjectId,
      name: this.createForm.value.name,
      description: this.createForm.value.description || '',
      createdById: userId || ''
    }).subscribe({
      next: () => {
        this.showCreateModal = false;
        this.createForm.reset();
        this.creating = false;
        this.createError = '';
        this.loadSuites();
      },
      error: (err) => {
        this.createError = err?.error?.fail_Messages || 'Failed to create test suite.';
        this.creating = false;
      }
    });
  }

  confirmDelete(suite: TestSuiteDto): void {
    if (this.isViewerRole) {
      this.errorMessage = 'Viewer role is read-only and cannot delete test suites.';
      return;
    }

    this.suiteToDelete = suite;
  }

  deleteSuite(): void {
    if (this.isViewerRole) {
      this.errorMessage = 'Viewer role is read-only and cannot delete test suites.';
      this.suiteToDelete = null;
      return;
    }

    if (!this.suiteToDelete) return;

    this.deleting = true;
    this.testSuiteService.deleteTestSuite(this.suiteToDelete.id).subscribe({
      next: () => {
        this.suiteToDelete = null;
        this.deleting = false;
        this.loadSuites();
      },
      error: () => {
        this.errorMessage = 'Failed to delete test suite.';
        this.deleting = false;
      }
    });
  }

  openBuilder(suite: TestSuiteDto): void {
    this.router.navigate(['/test-suites', 'builder', suite.id]);
  }

  openEditModal(suite: TestSuiteDto): void {
    if (this.isViewerRole) {
      this.errorMessage = 'Viewer role is read-only and cannot edit test suites.';
      return;
    }

    this.suiteToEdit = suite;
    this.editForm.patchValue({
      name: suite.name,
      description: suite.description || ''
    });
  }

  editSuite(): void {
    if (this.isViewerRole) {
      this.errorMessage = 'Viewer role is read-only and cannot edit test suites.';
      this.suiteToEdit = null;
      return;
    }

    if (this.editForm.invalid || !this.suiteToEdit) return;

    this.editing = true;
    this.testSuiteService.updateTestSuite(this.suiteToEdit.id, {
      id: this.suiteToEdit.id,
      name: this.editForm.value.name,
      description: this.editForm.value.description || ''
    }).subscribe({
      next: () => {
        this.suiteToEdit = null;
        this.editing = false;
        this.loadSuites();
      },
      error: () => {
        this.errorMessage = 'Failed to update test suite.';
        this.editing = false;
      }
    });
  }

  getTimeAgo(dateStr: string | null): string {
    if (!dateStr) return 'Never';
    const date = new Date(dateStr);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    const diffHours = Math.floor(diffMins / 60);
    if (diffHours < 24) return `${diffHours}h ago`;
    const diffDays = Math.floor(diffHours / 24);
    if (diffDays < 30) return `${diffDays}d ago`;
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
  }
}


