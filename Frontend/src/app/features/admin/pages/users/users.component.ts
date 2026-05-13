import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, forkJoin, of } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { catchError } from 'rxjs/operators';
import { UserService, User, UserDto, CreateUserDto } from '../../../../core/services/user.service';
import { TranslatePipe } from '../../../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-users',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe],
  templateUrl: './users.component.html',
  styleUrls: ['./users.component.scss']
})
export class UsersComponent implements OnInit, OnDestroy {

  users: User[] = [];
  filteredUsers: User[] = [];
  selectedUsers: string[] = [];

  isLoading = false;
  errorMessage = '';
  selectedRole = '';
  selectedStatus = '';
  searchKeyword = '';

  totalUsers = 0;
  activeSessions = 0;

  currentPage = 1;
  pageSize = 10;
  totalPages = 1;
  pages: number[] = [];

  showEditModal = false;
  editingUser: User | null = null;
  editForm: UserDto = { firstName: '', lastName: '', email: '', isActive: true };

  showRolesModal = false;
  editingRolesUser: User | null = null;
  selectedRoles: string[] = [];

  showInviteModal = false;
  inviteError = '';
  inviteForm: CreateUserDto = {
    firstName: '',
    lastName: '',
    email: '',
    password: ''
  };

  availableRoles = ['Admin', 'Manager', 'Tester', 'Viewer'];

  Math = Math;

  // Fix memory leak
  private destroy$ = new Subject<void>();

  constructor(private userService: UserService) {}

  ngOnInit(): void {
    this.loadUsers();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  loadUsers(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.userService.getAllUsers()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          if (response.status === 200 && response.resultat) {
            this.users = response.resultat;
            this.filteredUsers = [...this.users];
            this.totalUsers = this.users.length;
            this.activeSessions = this.users.filter(u => u.isActive).length;
            this.buildPages();
          }
          this.isLoading = false;
        },
        error: (error) => {
          this.errorMessage = error.message || 'Failed to load users';
          this.isLoading = false;
        }
      });
  }

  onSearch(): void {
    if (this.searchKeyword.trim()) {
      this.userService.searchUsers(this.searchKeyword)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: (response) => {
            if (response.status === 200 && response.resultat) {
              this.filteredUsers = response.resultat;
            }
          },
          error: (error) => {
            this.errorMessage = error.message;
          }
        });
    } else {
      this.onFilterChange();
    }
  }

  buildPages(): void {
    this.totalPages = Math.ceil(this.filteredUsers.length / this.pageSize) || 1;
    this.pages = Array.from({ length: this.totalPages }, (_, i) => i + 1);
  }

  onFilterChange(): void {
    this.filteredUsers = this.users.filter(user => {
      const roleMatch = this.selectedRole ? user.roles.includes(this.selectedRole) : true;
      const statusMatch = this.selectedStatus !== ''
        ? user.isActive === (this.selectedStatus === 'true')
        : true;
      return roleMatch && statusMatch;
    });
    this.buildPages();
    this.currentPage = 1;
  }

  clearFilters(): void {
    this.searchKeyword = '';
    this.selectedRole = '';
    this.selectedStatus = '';
    this.filteredUsers = [...this.users];
    this.currentPage = 1;
    this.buildPages();
  }

  getRoleCount(role: string): number {
    return this.users.filter(user => user.roles.includes(role)).length;
  }

  bulkDeactivateSelected(): void {
    const activeSelectedUsers = this.users.filter(
      u => this.selectedUsers.includes(u.id) && u.isActive
    );

    if (!activeSelectedUsers.length) return;

    this.isLoading = true;

    forkJoin(
      activeSelectedUsers.map(user =>
        this.userService.toggleStatus(user.id).pipe(catchError(() => of(null)))
      )
    )
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        activeSelectedUsers.forEach(u => {
          u.isActive = false;
          const filteredUser = this.filteredUsers.find(fu => fu.id === u.id);
          if (filteredUser) filteredUser.isActive = false;
        });

        this.activeSessions = this.users.filter(u => u.isActive).length;
        this.selectedUsers = [];
        this.onFilterChange();
        this.isLoading = false;
      });
  }

  toggleSelectAll(event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.selectedUsers = checked ? this.filteredUsers.map(u => u.id) : [];
  }

  toggleSelect(id: string): void {
    if (this.selectedUsers.includes(id)) {
      this.selectedUsers = this.selectedUsers.filter(uid => uid !== id);
    } else {
      this.selectedUsers.push(id);
    }
  }

  isSelected(id: string): boolean {
    return this.selectedUsers.includes(id);
  }

  getInitials(firstName: string, lastName: string): string {
    return `${firstName?.charAt(0) ?? ''}${lastName?.charAt(0) ?? ''}`.toUpperCase();
  }

  getAvatarClass(roles: string[]): string {
    const role = roles[0];
    switch (role) {
      case 'Admin':   return 'bg-blue-100 text-blue-800';
      case 'Manager': return 'bg-purple-100 text-purple-800';
      case 'Tester':  return 'bg-green-100 text-green-800';
      case 'Viewer':  return 'bg-slate-100 text-slate-600';
      default:        return 'bg-slate-100 text-slate-600';
    }
  }

  getRoleBadgeClass(roles: string[]): string {
    const role = roles[0];
    switch (role) {
      case 'Admin':   return 'bg-blue-100 text-blue-800';
      case 'Manager': return 'bg-purple-100 text-purple-800';
      case 'Tester':  return 'bg-green-100 text-green-800';
      default:        return 'bg-slate-100 text-slate-600';
    }
  }

  toggleStatus(user: User): void {
    this.userService.toggleStatus(user.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          user.isActive = !user.isActive;
          this.activeSessions = this.users.filter(u => u.isActive).length;
        },
        error: (error) => {
          this.errorMessage = error.message || 'Failed to toggle status';
        }
      });
  }

  openEditUser(user: User): void {
    this.editingUser = user;
    this.editForm = {
      firstName: user.firstName,
      lastName: user.lastName,
      email: user.email,
      isActive: user.isActive
    };
    this.showEditModal = true;
  }

  saveEditUser(): void {
    if (!this.editingUser) return;

    this.userService.updateUser(this.editingUser.id, this.editForm)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          if (response.status === 200) {
            const index = this.users.findIndex(u => u.id === this.editingUser!.id);
            if (index !== -1) {
              this.users[index] = { ...this.users[index], ...this.editForm };
              this.onFilterChange();
            }
            this.closeEditModal();
          }
        },
        error: (error) => {
          this.errorMessage = error.message || 'Failed to update user';
        }
      });
  }

  closeEditModal(): void {
    this.showEditModal = false;
    this.editingUser = null;
  }

  openEditRoles(user: User): void {
    this.editingRolesUser = user;
    this.selectedRoles = [...(user.roles || [])];
    this.showRolesModal = true;
  }

  toggleRole(role: string): void {
    // Un seul rôle à la fois
    this.selectedRoles = [role];
  }

  saveRoles(): void {
    if (!this.editingRolesUser) return;

    this.userService.updateUserRoles(this.editingRolesUser.id, this.selectedRoles)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          if (response.status === 200) {
            const index = this.users.findIndex(u => u.id === this.editingRolesUser!.id);
            if (index !== -1) {
              this.users[index].roles = [...this.selectedRoles];
            }
            const filteredIndex = this.filteredUsers.findIndex(u => u.id === this.editingRolesUser!.id);
            if (filteredIndex !== -1) {
              this.filteredUsers[filteredIndex].roles = [...this.selectedRoles];
            }
            this.closeRolesModal();
          }
        },
        error: (error) => {
          this.errorMessage = error.message || 'Failed to update roles';
        }
      });
  }

  closeRolesModal(): void {
    this.showRolesModal = false;
    this.editingRolesUser = null;
  }

  openInviteModal(): void {
    this.inviteForm = { firstName: '', lastName: '', email: '', password: '' };
    this.inviteError = '';
    this.showInviteModal = true;
  }

  closeInviteModal(): void {
    this.inviteError = '';
    this.showInviteModal = false;
  }

  submitInviteUser(): void {
    if (!this.inviteForm.firstName.trim() || !this.inviteForm.lastName.trim() ||
        !this.inviteForm.email.trim() || !this.inviteForm.password.trim()) {
      this.inviteError = 'Please fill all fields before submitting.';
      return;
    }

    this.inviteError = '';
    this.userService.inviteUser(this.inviteForm)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          if (response.status === 200) {
            this.closeInviteModal();
            this.loadUsers();
            return;
          }

          this.inviteError = response.fail_Messages || 'Failed to invite user.';
        },
        error: (error) => {
          const body = error?.error;
          if (body?.errors) {
            this.inviteError = (Object.values(body.errors) as string[][]).flat().join(' ');
          } else if (body?.message) {
            this.inviteError = body.message;
          } else if (typeof body === 'string' && body.trim()) {
            this.inviteError = body;
          } else {
            this.inviteError = 'Failed to create user. Check email and password requirements.';
          }
        }
      });
  }

  getPaginatedUsers(): User[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.filteredUsers.slice(start, start + this.pageSize);
  }

  changePage(page: number): void {
    if (page < 1 || page > this.totalPages) return;
    this.currentPage = page;
  }
}