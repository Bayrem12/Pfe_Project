import { Component, OnDestroy, OnInit, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslatePipe } from '../../../../shared/pipes/translate.pipe';
import { Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime } from 'rxjs';
import { filter, switchMap, takeUntil, tap } from 'rxjs/operators';
import { ProjectService } from '../../../../core/services/project.service';
import { toSlug } from '../../../../core/services/project.service';
import { Project } from '../../../../core/models/project.model';
import { ResponseHttp } from '../../../../core/models/response-http.model';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../../environments/environment';
import { AuthService } from '../../../../core/services/auth.service';
import { ConfirmService } from '../../../../core/services/confirm.service';
import { TranslationService } from '../../../../core/services/translation.service';

interface User {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  roles?: string[];
}
// allow userName as optional because API may return it instead of firstName/lastName
interface UserWithOptionalName extends User {
  userName?: string;
}

type ProjectStatusValue = boolean | string | number | undefined;

type ProjectStatusSource = Partial<Project> & {
  IsActive?: ProjectStatusValue;
  status?: ProjectStatusValue;
  Status?: ProjectStatusValue;
  Url?: string;
};

type ProjectCollection = ProjectStatusSource[] | {
  items?: ProjectStatusSource[];
  Items?: ProjectStatusSource[];
};

type ProjectListResponse = ResponseHttp<ProjectCollection> & {
  Resultat?: ProjectCollection;
};

type ProjectResponse = ResponseHttp<ProjectStatusSource> & {
  Resultat?: ProjectStatusSource;
};

type UserCollection = UserWithOptionalName[] | {
  items?: UserWithOptionalName[];
  Items?: UserWithOptionalName[];
};

type UsersResponse = UserWithOptionalName[] | (ResponseHttp<UserCollection> & {
  data?: UserCollection;
});

type ProjectUpdatePayload = {
  ProjectId: string;
  Name: string;
  Description: string;
  Url?: string;
  IsActive: boolean;
};

type ProjectMemberPayload = {
  UserId: string;
  Role: number;
};

@Component({
  selector: 'app-project-list',
  standalone: true,
  imports: [TranslatePipe, CommonModule, RouterModule, FormsModule],
  templateUrl: './project-list.component.html',
  styleUrl: './project-list.component.scss'
})
export class ProjectListComponent implements OnInit, OnDestroy {
  projects: Project[] = [];
  filteredProjects: Project[] = [];
  isLoading = true;
  activeFilter: 'all' | 'active' | 'archived' = 'all';
  viewMode: 'grid' | 'list' = 'grid';
  sortBy = 'recently-modified';
  sortDropdownOpen = false;
  openActionsMenuProjectId: string | null = null;
  memberSearchQuery = '';

  // Modal state - Edit Project
  showEditProjectModal = false;
  isUpdatingProject = false;
  editProjectError = '';
  editProjectForm = {
    projectId: '',
    name: '',
    description: '',
    isActive: true
  };

  // Modal state - Add Member (Multi-select)
  showAddMemberModal = false;
  selectedProject: Project | null = null;
  availableUsers: UserWithOptionalName[] = [];
  isSearchingUsers = false;
  isAddingMember = false;
  addMemberError = '';
  selectedMembers: Array<{userId: string, firstName: string, lastName: string, role: string}> = [];
  private memberSearch$ = new Subject<string>();

  // Modal state - Members List
  showMembersListModal = false;
  selectedProjectForMembers: Project | null = null;
  deletingMemberId: string | null = null;
  membersListError = '';

  userId = '';
  currentRole = 'viewer';
  get isViewerOrManager(): boolean {
    return this.currentRole === 'viewer' || this.currentRole === 'tester';
  }

  private apiUrl = environment.apiUrl;
  private destroy$ = new Subject<void>();

  constructor(
    private projectService: ProjectService,
    private http: HttpClient,
    private authService: AuthService,
    private confirmService: ConfirmService,
    private translationService: TranslationService,
    private router: Router
  ) {}

  @HostListener('document:click')
  onDocumentClick(): void {
    this.openActionsMenuProjectId = null;
    this.sortDropdownOpen = false;
  }

  ngOnInit(): void {
    this.initCurrentUserContextAndProjects();
    this.initMemberSearch();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initCurrentUserContextAndProjects(): void {
    this.authService.currentUser$
      .pipe(
        takeUntil(this.destroy$),
        filter(user => !!user?.id),
        tap(user => {
          this.userId = user!.id;
          this.currentRole = (user?.roles?.[0] || 'viewer').toLowerCase();
        }),
        switchMap(() => this.projectService.getProjects(this.userId))
      )
      .subscribe({
        next: (response: ProjectListResponse) => {
          const data = response.resultat;
          if (Array.isArray(data)) {
            this.projects = data.map((p) => this.normalizeProjectStatus(p));
          } else if (data && Array.isArray(data.items)) {
            this.projects = data.items.map((p) => this.normalizeProjectStatus(p));
          } else if (data && Array.isArray(data.Items)) {
            this.projects = data.Items.map((p) => this.normalizeProjectStatus(p));
          } else {
            this.projects = [];
          }
          this.applyFilter();
          this.isLoading = false;
        },
        error: (error) => {
          console.error('Erreur lors du chargement des projets:', error);
          this.projects = [];
          this.applyFilter();
          this.isLoading = false;
        }
      });
  }

  canManageProjects(): boolean {
    return this.currentRole === 'admin' || this.currentRole === 'manager';
  }

  canCreateProjects(): boolean {
    return this.canManageProjects();
  }

  loadProjects(): void {
    this.isLoading = true;
    this.projectService.getProjects(this.userId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response: ProjectListResponse) => {
          const data = response.resultat;
          if (Array.isArray(data)) {
            this.projects = data.map((p) => this.normalizeProjectStatus(p));
          } else if (data && Array.isArray(data.items)) {
            this.projects = data.items.map((p) => this.normalizeProjectStatus(p));
          } else if (data && Array.isArray(data.Items)) {
            this.projects = data.Items.map((p) => this.normalizeProjectStatus(p));
          } else {
            this.projects = [];
          }
          this.applyFilter();
          this.isLoading = false;
        },
        error: (error) => {
          console.error('Erreur lors du chargement des projets:', error);
          this.projects = [];
          this.applyFilter();
          this.isLoading = false;
        }
      });
  }

  private normalizeProjectStatus(project: ProjectStatusSource): Project {
    const rawStatus = project?.isActive ?? project?.IsActive ?? project?.status ?? project?.Status;

    const isActive =
      rawStatus === true ||
      rawStatus === 'true' ||
      rawStatus === 1 ||
      rawStatus === '1' ||
      rawStatus === 'Active' ||
      rawStatus === 'ACTIVE';

    return {
      ...project,
      url: project.url ?? project.Url ?? '',
      isActive
    } as Project;
  }

  private initMemberSearch(): void {
    this.memberSearch$
      .pipe(
        debounceTime(300),
        takeUntil(this.destroy$)
      )
      .subscribe(keyword => this.fetchUsers(keyword));
  }

  private fetchUsers(keyword: string): void {
    this.isSearchingUsers = true;
    const url = `${this.apiUrl}/user/search?keyword=${encodeURIComponent(keyword)}`;
    console.log('[AddMember] fetching users from:', url);
    this.http.get<UsersResponse>(url)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          console.log('[AddMember] raw response:', response);
          const raw = response as any;
          const data = Array.isArray(raw)
            ? raw
            : (raw?.resultat ?? raw?.Resultat ?? raw?.data ?? raw?.Data ?? []);
          this.availableUsers = Array.isArray(data) ? data : [];
          console.log('[AddMember] availableUsers length:', this.availableUsers.length, this.availableUsers);
          this.isSearchingUsers = false;
        },
        error: (err) => {
          console.error('[AddMember] search error:', err);
          this.availableUsers = [];
          this.isSearchingUsers = false;
        }
      });
  }

  loadUsers(): void {
    // Called from (ngModelChange) on the search input — debounced
    this.memberSearch$.next(this.memberSearchQuery);
  }

  applyFilter(): void {
    switch (this.activeFilter) {
      case 'active':
        this.filteredProjects = this.projects.filter(p => p.isActive);
        break;
      case 'archived':
        this.filteredProjects = this.projects.filter(p => !p.isActive);
        break;
      default:
        this.filteredProjects = [...this.projects];
    }
    this.applySorting();
  }

  applySorting(): void {
    switch (this.sortBy) {
      case 'name':
        this.filteredProjects.sort((a, b) => (a.name || '').localeCompare(b.name || ''));
        break;
      case 'created':
        this.filteredProjects.sort((a, b) => {
          const da = a.createdDate ? new Date(a.createdDate).getTime() : 0;
          const db = b.createdDate ? new Date(b.createdDate).getTime() : 0;
          return db - da;
        });
        break;
      case 'recently-modified':
      default:
        // Already sorted by backend (ModifiedDate desc), keep as-is
        break;
    }
  }

  setSortBy(value: string): void {
    this.sortBy = value;
    this.sortDropdownOpen = false;
    this.applyFilter();
  }

  toggleSortDropdown(event: Event): void {
    event.stopPropagation();
    this.sortDropdownOpen = !this.sortDropdownOpen;
  }

  get sortLabel(): string {
    switch (this.sortBy) {
      case 'name': return 'Name';
      case 'created': return 'Created Date';
      default: return 'Recently Modified';
    }
  }

  setFilter(filter: 'all' | 'active' | 'archived'): void {
    this.activeFilter = filter;
    this.applyFilter();
  }

  setViewMode(mode: 'grid' | 'list'): void {
    this.viewMode = mode;
  }

  toggleProjectActionsMenu(projectId: string, event: Event): void {
    if (!this.canManageProjects()) {
      return;
    }

    event.stopPropagation();
    event.preventDefault();
    this.openActionsMenuProjectId = this.openActionsMenuProjectId === projectId ? null : projectId;
  }

  editProject(project: Project, event: Event): void {
    if (!this.canManageProjects()) {
      return;
    }

    event.stopPropagation();
    event.preventDefault();
    this.openActionsMenuProjectId = null;

    this.editProjectForm = {
      projectId: project.id,
      name: project.name || '',
      description: project.description || '',
      isActive: !!project.isActive
    };

    this.editProjectError = '';
    this.showEditProjectModal = true;

    // Load latest project details to prefill modal with freshest data
    this.projectService.getProjectById(project.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response: ProjectResponse) => {
          const data = this.normalizeProjectStatus(response?.resultat || response?.Resultat || response);
          this.editProjectForm = {
            projectId: data.id || project.id,
            name: data.name || '',
            description: data.description || '',
            isActive: !!data.isActive
          };
        },
        error: (error) => {
          console.error('Erreur lors du chargement du projet Ã  modifier:', error);
        }
      });
  }

  closeEditProjectModal(): void {
    this.showEditProjectModal = false;
    this.isUpdatingProject = false;
    this.editProjectError = '';
    this.editProjectForm = {
      projectId: '',
      name: '',
      description: '',
      isActive: true
    };
  }

  saveProjectChanges(): void {
    if (!this.canManageProjects()) {
      this.editProjectError = 'You do not have permission to edit projects.';
      return;
    }

    if (!this.editProjectForm.projectId) {
      this.editProjectError = 'Project id is required';
      return;
    }

    if (!this.editProjectForm.name.trim()) {
      this.editProjectError = 'Project name is required';
      return;
    }

    this.isUpdatingProject = true;
    this.editProjectError = '';

    const payload: ProjectUpdatePayload = {
      ProjectId: this.editProjectForm.projectId,
      Name: this.editProjectForm.name.trim(),
      Description: this.editProjectForm.description?.trim() || '',
      IsActive: !!this.editProjectForm.isActive
    };

    this.projectService.updateProject(this.editProjectForm.projectId, payload as unknown as import('../../../../core/models/project.model').UpdateProjectRequest)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.isUpdatingProject = false;
          this.closeEditProjectModal();
          this.loadProjects();
        },
        error: (error) => {
          console.error('Erreur lors de la mise Ã  jour du projet:', error);
          this.editProjectError = error?.error?.fail_Messages || error?.error?.Fail_Messages || 'Failed to update project';
          this.isUpdatingProject = false;
        }
      });
  }

  toggleProjectStatus(project: Project, event: Event): void {
    if (!this.canManageProjects()) {
      return;
    }

    event.stopPropagation();
    event.preventDefault();

    const updatedStatus = !project.isActive;
    const payload: ProjectUpdatePayload = {
      ProjectId: project.id,
      Name: project.name,
      Description: project.description,
      Url: project.url || '',
      IsActive: updatedStatus
    };

    this.projectService.updateProject(project.id, payload as unknown as import('../../../../core/models/project.model').UpdateProjectRequest)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          project.isActive = updatedStatus;
          this.applyFilter();
        },
        error: (error) => {
          console.error('Erreur lors du changement de statut du projet:', error);
        }
      });
  }

  openAddMemberModal(project: Project, event: Event): void {
    console.log('[AddMember] openAddMemberModal called. currentRole=', this.currentRole, 'canManage=', this.canManageProjects());
    if (!this.canManageProjects()) {
      return;
    }

    event.stopPropagation();
    event.preventDefault();
    this.selectedProject = project;
    this.addMemberError = '';
    this.selectedMembers = [];
    this.memberSearchQuery = '';
    this.availableUsers = [];
    this.showAddMemberModal = true;
    // Direct call (no debounce) so users load immediately when modal opens
    this.fetchUsers('');
  }

  closeAddMemberModal(): void {
    this.showAddMemberModal = false;
    this.selectedProject = null;
    this.addMemberError = '';
    this.selectedMembers = [];
    this.availableUsers = [];
  }

  getFilteredAvailableUsers(): UserWithOptionalName[] {
    const existingMemberIds = new Set(
      (this.selectedProject?.members || []).map(m => m.userId || m.id)
    );
    const selectedIds = new Set(this.selectedMembers.map(m => m.userId));
    return this.availableUsers.filter(u =>
      !existingMemberIds.has(u.id) &&
      !selectedIds.has(u.id) &&
      !u.roles?.some(r => r.toLowerCase() === 'admin')
    );
  }

  selectUserToAdd(user: UserWithOptionalName): void {
    this.selectedMembers.push({
      userId: user.id,
      firstName: user.firstName || user.userName || user.email || '',
      lastName: user.lastName || '',
      role: user.roles?.[0] || 'Viewer'
    });
    this.memberSearchQuery = '';
  }

  selectUserToAddFromKeyboard(user: UserWithOptionalName, event: Event): void {
    if (!this.isKeyboardActivation(event)) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    this.selectUserToAdd(user);
  }

  removeFromSelection(index: number): void {
    this.selectedMembers.splice(index, 1);
  }

  addMembers(): void {
    if (!this.canManageProjects()) {
      this.addMemberError = 'You do not have permission to add members.';
      return;
    }

    if (this.selectedMembers.length === 0) {
      this.addMemberError = 'Please select at least one user';
      return;
    }

    if (!this.selectedProject) {
      return;
    }

    this.isAddingMember = true;
    this.addMemberError = '';

    const roleMap: { [key: string]: number } = {
      'Admin': 0,
      'Manager': 1,
      'Tester': 2,
      'Viewer': 3
    };

    let completed = 0;
    let errors: string[] = [];
    const total = this.selectedMembers.length;

    for (const member of this.selectedMembers) {
      const roleValue = roleMap[member.role] ?? 3;
      const payload: ProjectMemberPayload = {
        UserId: member.userId,
        Role: roleValue
      };

      this.http.post<void>(
        `${this.apiUrl}/projet/${this.selectedProject!.id}/members`,
        payload
      )
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            completed++;
            if (completed === total) {
              this.isAddingMember = false;
              if (errors.length) {
                this.addMemberError = `Added ${total - errors.length}/${total}. Errors: ${errors.join(', ')}`;
              } else {
                this.closeAddMemberModal();
              }
              this.loadProjects();
            }
          },
          error: (error) => {
            completed++;
            errors.push(member.firstName);
            if (completed === total) {
              this.isAddingMember = false;
              this.addMemberError = `Failed to add: ${errors.join(', ')}`;
              this.loadProjects();
            }
          }
        });
    }
  }

  // Members List Modal
  openMembersListModal(project: Project, event: Event): void {
    event.stopPropagation();
    event.preventDefault();
    this.selectedProjectForMembers = project;
    this.showMembersListModal = true;
  }

  getProjectSlug(project: Project): string {
    return toSlug(project.name);
  }

  openProjectFromKeyboard(project: Project, event: Event): void {
    if (!this.isKeyboardActivation(event)) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    void this.router.navigate(['/projects', toSlug(project.name)], { state: { projectId: project.id } });
  }

  openCreateProjectFromKeyboard(event: Event): void {
    if (!this.isKeyboardActivation(event)) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    void this.router.navigate(['/projects/create']);
  }

  openMembersListModalFromKeyboard(project: Project, event: Event): void {
    if (!this.isKeyboardActivation(event)) {
      return;
    }

    this.openMembersListModal(project, event);
  }

  private isKeyboardActivation(event: Event): boolean {
    const keyboardEvent = event as KeyboardEvent;
    return keyboardEvent.key === 'Enter' || keyboardEvent.key === ' ' || keyboardEvent.key === 'Spacebar';
  }

  closeMembersListModal(): void {
    this.showMembersListModal = false;
    this.selectedProjectForMembers = null;
    this.deletingMemberId = null;
    this.membersListError = '';
  }

  confirmDeleteMember(userId: string, event: Event): void {
    event.stopPropagation();
    this.deletingMemberId = this.deletingMemberId === userId ? null : userId;
    this.membersListError = '';
  }

  removeMember(userId: string, event: Event): void {
    event.stopPropagation();
    if (!this.selectedProjectForMembers || !this.canManageProjects()) return;
    this.membersListError = '';

    this.http.delete<void>(
      `${this.apiUrl}/projet/${this.selectedProjectForMembers.id}/members/${userId}`
    )
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.deletingMemberId = null;
          if (this.selectedProjectForMembers?.members) {
            this.selectedProjectForMembers.members = this.selectedProjectForMembers.members.filter(
              m => (m.userId || m.id) !== userId
            );
          }
          this.loadProjects();
        },
        error: (error) => {
          this.deletingMemberId = null;
          this.membersListError = error?.error?.fail_Messages || error?.error?.Fail_Messages || 'Failed to remove member. Please try again.';
          console.error('Error removing member:', error);
        }
      });
  }

  getRoleName(role: number | string): string {
    const roleNames: { [key: number]: string } = {
      0: 'Admin',
      1: 'Admin',
      2: 'Tester',
      3: 'Viewer'
    };
    if (typeof role === 'number') {
      return roleNames[role] || 'Unknown';
    }
    return role || 'Unknown';
  }

  async deleteProject(id: string, event: Event): Promise<void> {
    if (!this.canManageProjects()) {
      return;
    }

    event.stopPropagation();
    this.openActionsMenuProjectId = null;

    const project = this.projects.find(p => p.id === id);
    const ok = await this.confirmService.open({
      title: this.translationService.t('project.list.confirmDeleteTitle', project?.name || ''),
      description: this.translationService.t('delete.dialog.defaultDesc'),
      confirmLabel: this.translationService.t('action.delete'),
      cancelLabel: this.translationService.t('action.cancel'),
    });
    if (!ok) return;

    this.projectService.deleteProject(id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.projects = this.projects.filter(p => p.id !== id);
          this.applyFilter();
        },
        error: (error) => {
          console.error('Erreur lors de la suppression:', error);
        }
      });
  }
}

