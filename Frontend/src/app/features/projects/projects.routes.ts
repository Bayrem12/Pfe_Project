import { Routes } from '@angular/router';
import { ProjectListComponent } from './pages/project-list/project-list.component';
import { ProjectCreateComponent } from './pages/project-create/project-create.component';
import { ProjectDetailComponent } from './pages/project-detail/project-detail.component';
import { ModuleCreateComponent } from './module-create/module-create.component';

export const projectsRoutes: Routes = [
  {
    path: '',
    component: ProjectListComponent
  },
  {
    path: 'create',
    component: ProjectCreateComponent
  },
  {
    path: ':projectId/modules/create',
    component: ModuleCreateComponent
  },
  {
    path: ':slug',
    component: ProjectDetailComponent
  }
];