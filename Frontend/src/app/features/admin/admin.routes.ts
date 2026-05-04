import { Routes } from '@angular/router';
import { AdminDashboardComponent } from './pages/admin-dashboard/admin-dashboard.component';
import { UsersComponent } from './pages/users/users.component';
import { authGuard } from '../../core/guards/auth.guard';
import { roleGuard } from '../../core/guards/role.guard';

export const adminRoutes: Routes = [
  {
    path: '',
    component: UsersComponent,
    canActivate: [authGuard, roleGuard]
  },
  {
    path: 'dashboard',
    component: AdminDashboardComponent,
    canActivate: [authGuard, roleGuard]
  }
];