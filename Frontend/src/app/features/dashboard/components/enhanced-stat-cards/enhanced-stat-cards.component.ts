import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DashboardSummary } from '../../models/dashboard.model';

@Component({
  selector: 'app-enhanced-stat-cards',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6 gap-4 mb-6">
      <!-- Total Projects Card -->
      <div class="bg-white rounded-lg shadow-sm border p-6 hover:shadow-md transition-shadow">
        <div class="flex items-center justify-between mb-2">
          <div class="flex items-center space-x-2">
            <div class="w-10 h-10 bg-blue-100 rounded-lg flex items-center justify-center">
              <span class="material-symbols-outlined text-blue-600 text-xl">folder</span>
            </div>
            <div>
              <p class="text-xs font-medium text-gray-500 uppercase tracking-wide">Total Projects</p>
              <p class="text-2xl font-bold text-gray-900">{{ summary?.totalProjects?.value || 0 }}</p>
            </div>
          </div>
        </div>
        <div class="flex items-center justify-between">
          <div class="flex items-center space-x-1">
            <span class="text-xs text-gray-500">Active:</span>
            <span class="text-sm font-semibold text-green-600">{{ summary?.totalProjects?.active || 0 }}</span>
          </div>
          <div class="flex items-center space-x-1" [ngClass]="getTrendClass(summary?.totalProjects?.trend)">
            <span class="material-symbols-outlined text-xs">{{ getTrendIcon(summary?.totalProjects?.trend) }}</span>
            <span class="text-xs font-medium">{{ summary?.totalProjects?.change || 0 }}%</span>
          </div>
        </div>
      </div>

      <!-- Total Scenarios Card -->
      <div class="bg-white rounded-lg shadow-sm border p-6 hover:shadow-md transition-shadow">
        <div class="flex items-center justify-between mb-2">
          <div class="flex items-center space-x-2">
            <div class="w-10 h-10 bg-purple-100 rounded-lg flex items-center justify-center">
              <span class="material-symbols-outlined text-purple-600 text-xl">psychology</span>
            </div>
            <div>
              <p class="text-xs font-medium text-gray-500 uppercase tracking-wide">Total Scenarios</p>
              <p class="text-2xl font-bold text-gray-900">{{ summary?.totalScenarios?.value || 0 }}</p>
            </div>
          </div>
        </div>
        <div class="flex items-center justify-end">
          <div class="flex items-center space-x-1" [ngClass]="getTrendClass(summary?.totalScenarios?.trend)">
            <span class="material-symbols-outlined text-xs">{{ getTrendIcon(summary?.totalScenarios?.trend) }}</span>
            <span class="text-xs font-medium">{{ summary?.totalScenarios?.change || 0 }}%</span>
          </div>
        </div>
      </div>

      <!-- Total Executions Card -->
      <div class="bg-white rounded-lg shadow-sm border p-6 hover:shadow-md transition-shadow">
        <div class="flex items-center justify-between mb-2">
          <div class="flex items-center space-x-2">
            <div class="w-10 h-10 bg-indigo-100 rounded-lg flex items-center justify-center">
              <span class="material-symbols-outlined text-indigo-600 text-xl">play_circle</span>
            </div>
            <div>
              <p class="text-xs font-medium text-gray-500 uppercase tracking-wide">Total Executions</p>
              <p class="text-2xl font-bold text-gray-900">{{ summary?.totalExecutions?.value || 0 }}</p>
            </div>
          </div>
        </div>
        <div class="flex items-center justify-between">
          <div class="flex items-center space-x-1">
            <span class="text-xs text-gray-500">Pending:</span>
            <span class="text-sm font-semibold text-yellow-600">{{ summary?.totalExecutions?.pending || 0 }}</span>
          </div>
          <div class="flex items-center space-x-1" [ngClass]="getTrendClass(summary?.totalExecutions?.trend)">
            <span class="material-symbols-outlined text-xs">{{ getTrendIcon(summary?.totalExecutions?.trend) }}</span>
            <span class="text-xs font-medium">{{ summary?.totalExecutions?.change || 0 }}%</span>
          </div>
        </div>
      </div>

      <!-- Passed Runs Card -->
      <div class="bg-white rounded-lg shadow-sm border p-6 hover:shadow-md transition-shadow">
        <div class="flex items-center justify-between mb-2">
          <div class="flex items-center space-x-2">
            <div class="w-10 h-10 bg-green-100 rounded-lg flex items-center justify-center">
              <span class="material-symbols-outlined text-green-600 text-xl">check_circle</span>
            </div>
            <div>
              <p class="text-xs font-medium text-gray-500 uppercase tracking-wide">Passed Runs</p>
              <p class="text-2xl font-bold text-gray-900">{{ summary?.passedRuns?.count || 0 }}</p>
            </div>
          </div>
        </div>
        <div class="flex items-center justify-between">
          <div class="flex items-center space-x-1">
            <span class="text-xs text-gray-500">Pass rate:</span>
            <span class="text-sm font-semibold text-green-600">{{ summary?.passedRuns?.percentage || 0 }}%</span>
          </div>
          <div class="flex items-center space-x-1" [ngClass]="getTrendClass(summary?.passedRuns?.trend)">
            <span class="material-symbols-outlined text-xs">{{ getTrendIcon(summary?.passedRuns?.trend) }}</span>
          </div>
        </div>
      </div>

      <!-- Failed Runs Card -->
      <div class="bg-white rounded-lg shadow-sm border p-6 hover:shadow-md transition-shadow"
           [ngClass]="summary?.failedRuns?.hasFailures ? 'border-red-200' : 'border-green-200'">
        <div class="flex items-center justify-between mb-2">
          <div class="flex items-center space-x-2">
            <div class="w-10 h-10 rounded-lg flex items-center justify-center"
                 [ngClass]="summary?.failedRuns?.hasFailures ? 'bg-red-100' : 'bg-green-100'">
              <span class="material-symbols-outlined text-xl"
                    [ngClass]="summary?.failedRuns?.hasFailures ? 'text-red-600' : 'text-green-600'">
                {{ summary?.failedRuns?.hasFailures ? 'cancel' : 'verified' }}
              </span>
            </div>
            <div>
              <p class="text-xs font-medium text-gray-500 uppercase tracking-wide">Failed Runs</p>
              <p class="text-2xl font-bold"
                 [ngClass]="summary?.failedRuns?.hasFailures ? 'text-red-700' : 'text-green-700'">
                {{ summary?.failedRuns?.count || 0 }}
              </p>
            </div>
          </div>
        </div>
        <div class="flex items-center justify-end">
          <span class="px-2 py-1 text-xs font-medium rounded-full"
                [ngClass]="summary?.failedRuns?.hasFailures ? 'bg-red-100 text-red-700' : 'bg-green-100 text-green-700'">
            {{ summary?.failedRuns?.hasFailures ? (summary?.failedRuns?.percentage || 0) + '% failure rate' : 'All passing' }}
          </span>
        </div>
      </div>

      <!-- Pending Runs Card -->
      <div class="bg-white rounded-lg shadow-sm border p-6 hover:shadow-md transition-shadow"
           [ngClass]="summary?.pendingRuns?.isUrgent ? 'border-amber-200' : ''">
        <div class="flex items-center justify-between mb-2">
          <div class="flex items-center space-x-2">
            <div class="w-10 h-10 rounded-lg flex items-center justify-center"
                 [ngClass]="summary?.pendingRuns?.isUrgent ? 'bg-amber-100' : 'bg-slate-100'">
              <span class="material-symbols-outlined text-xl"
                    [ngClass]="summary?.pendingRuns?.isUrgent ? 'text-amber-600' : 'text-slate-500'">schedule</span>
            </div>
            <div>
              <p class="text-xs font-medium text-gray-500 uppercase tracking-wide">Pending Runs</p>
              <p class="text-2xl font-bold"
                 [ngClass]="summary?.pendingRuns?.isUrgent ? 'text-amber-700' : 'text-gray-900'">
                {{ summary?.pendingRuns?.count || 0 }}
              </p>
            </div>
          </div>
        </div>
        <div class="flex items-center justify-end">
          <span class="px-2 py-1 text-xs font-medium rounded-full"
                [ngClass]="summary?.pendingRuns?.isUrgent ? 'bg-amber-100 text-amber-700' : 'bg-slate-100 text-slate-500'">
            {{ summary?.pendingRuns?.isUrgent ? 'Needs attention' : 'Queue clear' }}
          </span>
        </div>
      </div>
    </div>
  `,
  styleUrls: ['./enhanced-stat-cards.component.scss']
})
export class EnhancedStatCardsComponent {
  @Input() summary: DashboardSummary | null = null;

  getTrendClass(trend?: string): string {
    switch (trend) {
      case 'up': return 'text-green-600';
      case 'down': return 'text-red-600';
      case 'stable': return 'text-blue-600';
      default: return 'text-gray-400';
    }
  }

  getTrendIcon(trend?: string): string {
    switch (trend) {
      case 'up': return 'trending_up';
      case 'down': return 'trending_down';
      case 'stable': return 'trending_flat';
      default: return 'remove';
    }
  }

}
