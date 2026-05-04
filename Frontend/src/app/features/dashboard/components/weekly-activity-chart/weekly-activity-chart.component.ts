import { Component, Input, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgApexchartsModule, ApexChart, ApexAxisChartSeries, ApexXAxis, ApexDataLabels, ApexPlotOptions, ApexTooltip, ApexLegend, ApexYAxis } from 'ng-apexcharts';
import { ExecutionTrend } from '../../models/dashboard.model';

export type HeatmapChartOptions = {
  series: ApexAxisChartSeries;
  chart: ApexChart;
  xaxis: ApexXAxis;
  yaxis: ApexYAxis;
  dataLabels: ApexDataLabels;
  plotOptions: ApexPlotOptions;
  tooltip: ApexTooltip;
  legend: ApexLegend;
  colors: string[];
};

@Component({
  selector: 'app-weekly-activity-chart',
  standalone: true,
  imports: [CommonModule, NgApexchartsModule],
  template: `
    <div class="bg-white rounded-2xl shadow-sm p-6">
      <div class="flex items-center justify-between mb-4">
        <div>
          <h3 class="text-lg font-bold text-gray-800">Weekly Activity</h3>
          <p class="text-sm text-gray-500">Test execution volume by day</p>
        </div>
        <div class="flex items-center gap-2 text-xs text-gray-500">
          <span class="w-3 h-3 rounded bg-indigo-100"></span> Low
          <span class="w-3 h-3 rounded bg-indigo-300"></span> Medium
          <span class="w-3 h-3 rounded bg-indigo-600"></span> High
        </div>
      </div>
      
      <div class="grid grid-cols-7 gap-2" *ngIf="weeklyData.length > 0">
        <div *ngFor="let day of weeklyData" class="flex flex-col items-center gap-1.5">
          <span class="text-[10px] font-semibold text-gray-500 uppercase">{{ day.label }}</span>
          <div 
            class="w-full aspect-square rounded-xl flex flex-col items-center justify-center transition-all hover:scale-105 cursor-default"
            [ngStyle]="{ 'background-color': day.color }"
            [title]="day.label + ': ' + day.total + ' executions, ' + day.passed + ' passed, ' + day.failed + ' failed'">
            <span class="text-lg font-bold" [ngClass]="day.total > 5 ? 'text-white' : 'text-gray-600'">{{ day.total }}</span>
            <span class="text-[9px] font-medium" [ngClass]="day.total > 5 ? 'text-white/70' : 'text-gray-400'">tests</span>
          </div>
          <div class="flex gap-1 text-[9px]">
            <span class="text-green-600 font-semibold">{{ day.passed }}p</span>
            <span class="text-red-500 font-semibold">{{ day.failed }}f</span>
          </div>
        </div>
      </div>
      
      <div *ngIf="weeklyData.length === 0" class="flex flex-col items-center justify-center py-8 text-gray-400">
        <span class="material-symbols-outlined text-4xl mb-2">calendar_month</span>
        <p class="text-sm">No weekly data available</p>
      </div>
    </div>
  `
})
export class WeeklyActivityChartComponent implements OnInit, OnChanges {
  @Input() trends: ExecutionTrend[] = [];
  
  weeklyData: { label: string; total: number; passed: number; failed: number; color: string }[] = [];

  ngOnInit() {
    this.buildWeeklyData();
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['trends']) {
      this.buildWeeklyData();
    }
  }

  private buildWeeklyData() {
    if (!this.trends || this.trends.length === 0) {
      this.weeklyData = [];
      return;
    }

    // Aggregate by day of week from trends
    const dayMap: { [key: string]: { total: number; passed: number; failed: number } } = {
      'Mon': { total: 0, passed: 0, failed: 0 },
      'Tue': { total: 0, passed: 0, failed: 0 },
      'Wed': { total: 0, passed: 0, failed: 0 },
      'Thu': { total: 0, passed: 0, failed: 0 },
      'Fri': { total: 0, passed: 0, failed: 0 },
      'Sat': { total: 0, passed: 0, failed: 0 },
      'Sun': { total: 0, passed: 0, failed: 0 },
    };

    this.trends.forEach(t => {
      // Extract day abbreviation from the date string (e.g., "Mon 5")
      const dayAbbr = t.date.split(' ')[0];
      if (dayMap[dayAbbr]) {
        dayMap[dayAbbr].total += t.passed + t.failed;
        dayMap[dayAbbr].passed += t.passed;
        dayMap[dayAbbr].failed += t.failed;
      }
    });

    const maxTotal = Math.max(...Object.values(dayMap).map(d => d.total), 1);
    const days = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];

    this.weeklyData = days.map(day => ({
      label: day,
      total: dayMap[day].total,
      passed: dayMap[day].passed,
      failed: dayMap[day].failed,
      color: this.getHeatColor(dayMap[day].total, maxTotal)
    }));
  }

  private getHeatColor(value: number, max: number): string {
    if (value === 0) return '#f1f5f9';
    const intensity = value / max;
    if (intensity < 0.25) return '#e0e7ff';
    if (intensity < 0.5) return '#a5b4fc';
    if (intensity < 0.75) return '#6366f1';
    return '#4338ca';
  }
}
