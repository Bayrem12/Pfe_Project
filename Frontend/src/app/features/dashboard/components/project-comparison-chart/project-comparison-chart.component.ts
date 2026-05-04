import { Component, Input, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgApexchartsModule, ApexChart, ApexAxisChartSeries, ApexXAxis, ApexDataLabels, ApexStroke, ApexFill, ApexLegend, ApexYAxis, ApexMarkers, ApexPlotOptions } from 'ng-apexcharts';

export interface ProjectComparisonData {
  projectName: string;
  passRate: number;
  scenarioCount: number;
  executionCount: number;
  avgExecutionTime: number;
}

export type RadarChartOptions = {
  series: ApexAxisChartSeries;
  chart: ApexChart;
  xaxis: ApexXAxis;
  yaxis: ApexYAxis;
  dataLabels: ApexDataLabels;
  stroke: ApexStroke;
  fill: ApexFill;
  colors: string[];
  legend: ApexLegend;
  markers: ApexMarkers;
  plotOptions: ApexPlotOptions;
};

@Component({
  selector: 'app-project-comparison-chart',
  standalone: true,
  imports: [CommonModule, NgApexchartsModule],
  template: `
    <div class="bg-white rounded-2xl shadow-sm p-6">
      <div class="flex items-center justify-between mb-4">
        <div>
          <h3 class="text-lg font-bold text-gray-800">Project Comparison</h3>
          <p class="text-sm text-gray-500">Multi-dimensional quality metrics</p>
        </div>
      </div>
      
      <div *ngIf="projects.length === 0" class="flex flex-col items-center justify-center py-12 text-gray-400">
        <span class="material-symbols-outlined text-4xl mb-2">analytics</span>
        <p class="text-sm">No project data available</p>
      </div>
      
      <apx-chart
        *ngIf="chartOptions && projects.length > 0"
        [series]="chartOptions.series!"
        [chart]="chartOptions.chart!"
        [xaxis]="chartOptions.xaxis!"
        [yaxis]="chartOptions.yaxis!"
        [dataLabels]="chartOptions.dataLabels!"
        [stroke]="chartOptions.stroke!"
        [fill]="chartOptions.fill!"
        [colors]="chartOptions.colors!"
        [legend]="chartOptions.legend!"
        [markers]="chartOptions.markers!"
        [plotOptions]="chartOptions.plotOptions!"
      ></apx-chart>
    </div>
  `
})
export class ProjectComparisonChartComponent implements OnInit, OnChanges {
  @Input() projects: ProjectComparisonData[] = [];
  
  public chartOptions!: Partial<RadarChartOptions>;

  ngOnInit() {
    this.initializeChart();
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['projects'] && this.projects) {
      this.initializeChart();
    }
  }

  private initializeChart() {
    if (!this.projects || this.projects.length === 0) return;

    const colors = ['#6366f1', '#f59e0b', '#10b981', '#ef4444', '#8b5cf6'];
    
    // Normalize values to 0-100 scale for radar chart
    const maxScenarios = Math.max(...this.projects.map(p => p.scenarioCount), 1);
    const maxExecutions = Math.max(...this.projects.map(p => p.executionCount), 1);
    const maxTime = Math.max(...this.projects.map(p => p.avgExecutionTime), 1);

    const series = this.projects.slice(0, 5).map(project => ({
      name: project.projectName,
      data: [
        Math.round(project.passRate),
        Math.round((project.scenarioCount / maxScenarios) * 100),
        Math.round((project.executionCount / maxExecutions) * 100),
        Math.round(100 - (project.avgExecutionTime / maxTime) * 100), // Inverse: lower time = better
        Math.round(project.passRate * 0.7 + (project.executionCount / maxExecutions) * 30) // Composite health score
      ]
    }));

    this.chartOptions = {
      series,
      chart: {
        height: 320,
        type: 'radar',
        toolbar: { show: false },
        fontFamily: 'Inter, sans-serif'
      },
      colors: colors.slice(0, this.projects.length),
      xaxis: {
        categories: ['Pass Rate', 'Scenarios', 'Executions', 'Speed', 'Health'],
        labels: {
          style: { colors: '#64748b', fontSize: '11px' }
        }
      },
      yaxis: {
        show: false,
        max: 100
      },
      dataLabels: { enabled: false },
      stroke: {
        width: 2
      },
      fill: {
        opacity: 0.15
      },
      markers: {
        size: 3,
        hover: { size: 5 }
      },
      legend: {
        show: true,
        position: 'bottom',
        labels: { colors: '#64748b' },
        markers: {
          width: 8,
          height: 8
        }
      },
      plotOptions: {
        radar: {
          polygons: {
            strokeColors: '#e2e8f0',
            connectorColors: '#e2e8f0',
            fill: { colors: ['#f8fafc', '#ffffff'] }
          }
        }
      }
    };
  }
}
