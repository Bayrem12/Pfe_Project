import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-projects-list-page',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-8">
      <h1 class="text-3xl font-bold text-slate-900 mb-4">Projects</h1>
      <p class="text-slate-600">Projects list will be implemented by your teammates.</p>
    </div>
  `
})
export class ProjectsListPageComponent {}
