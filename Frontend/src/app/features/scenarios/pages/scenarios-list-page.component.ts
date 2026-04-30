import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-scenarios-list-page',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="p-8">
      <h1 class="text-3xl font-bold text-slate-900 mb-4">Scenarios</h1>
      <p class="text-slate-600">Scenarios list will be implemented by your teammates.</p>
    </div>
  `
})
export class ScenariosListPageComponent {}
