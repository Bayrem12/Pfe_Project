import { Component, Input, OnInit, OnChanges, SimpleChanges, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgApexchartsModule, ChartComponent, ApexChart, ApexAxisChartSeries, ApexXAxis, ApexDataLabels, ApexStroke, ApexGrid, ApexTooltip, ApexFill, ApexLegend, ApexYAxis, ApexMarkers, ApexAnnotations } from 'ng-apexcharts';
import { ExecutionTrend } from '../../models/dashboard.model';

export type LineChartOptions = {
  series: ApexAxisChartSeries;
  chart: ApexChart;
  xaxis: ApexXAxis;
  yaxis: ApexYAxis;
  dataLabels: ApexDataLabels;
  stroke: ApexStroke;
  grid: ApexGrid;
  tooltip: ApexTooltip;
  fill: ApexFill;
  colors: string[];
  legend: ApexLegend;
  markers: ApexMarkers;
  annotations: ApexAnnotations;
};

@Component({
  selector: 'app-pass-rate-trend-chart',
  standalone: true,
  imports: [CommonModule, NgApexchartsModule],
  template: `
    <div class="bg-white rounded-2xl shadow-sm p-6">
      <div class="flex items-center justify-between mb-4">
        <div>
          <h3 class="text-lg font-bold text-gray-800">Pass Rate Trend</h3>
          <p class="text-sm text-gray-500">Quality progression over time</p>
        </div>
        <div class="flex items-center gap-2">
          <span class="text-2xl font-extrabold" [ngClass]="currentPassRateClass">{{ currentPassRate }}%</span>
          <span class="material-symbols-outlined text-sm" [ngClass]="trendIconClass">{{ trendIcon }}</span>
        </div>
      </div>
      <apx-chart
        *ngIf="chartOptions"
        [series]="chartOptions.series!"
        [chart]="chartOptions.chart!"
        [xaxis]="chartOptions.xaxis!"
        [yaxis]="chartOptions.yaxis!"
        [dataLabels]="chartOptions.dataLabels!"
        [stroke]="chartOptions.stroke!"
        [grid]="chartOptions.grid!"
        [tooltip]="chartOptions.tooltip!"
        [fill]="chartOptions.fill!"
        [colors]="chartOptions.colors!"
        [markers]="chartOptions.markers!"
        [annotations]="chartOptions.annotations!"
      ></apx-chart>
    </div>
  `
})
export class PassRateTrendChartComponent implements OnInit, OnChanges {
  @Input() trends: ExecutionTrend[] = [];
  
  public chartOptions!: Partial<LineChartOptions>;
  currentPassRate = 0;
  currentPassRateClass = 'text-green-600';
  trendIcon = 'trending_up';
  trendIconClass = 'text-green-500';

  ngOnInit() {
    this.initializeChart();
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['trends'] && this.trends) {
      this.initializeChart();
    }
  }

  private initializeChart() {
    if (!this.trends || this.trends.length === 0) return;

    const categories = this.trends.map(t => t.date);
    const passRateData = this.trends.map(t => t.passRate);
    
    // Calculate current pass rate and trend
    this.currentPassRate = Math.round(passRateData[passRateData.length - 1] || 0);
    const prevRate = passRateData.length > 1 ? passRateData[passRateData.length - 2] : 0;
    
    if (this.currentPassRate >= prevRate) {
      this.trendIcon = 'trending_up';
      this.trendIconClass = 'text-green-500';
    } else {
      this.trendIcon = 'trending_down';
      this.trendIconClass = 'text-red-500';
    }
    
    this.currentPassRateClass = this.currentPassRate >= 85 ? 'text-green-600' : 
                                 this.currentPassRate >= 70 ? 'text-yellow-600' : 'text-red-600';

    this.chartOptions = {
      series: [{
        name: "Pass Rate",
        data: passRateData
      }],
      chart: {
        height: 250,
        type: "area",
        toolbar: { show: false },
        fontFamily: 'Inter, sans-serif',
        animations: {
          enabled: true,
          easing: 'easeinout',
          speed: 800
        }
      },
      colors: ['#6366f1'],
      fill: {
        type: 'gradient',
        gradient: {
          shade: 'light',
          type: 'vertical',
          shadeIntensity: 0.3,
          opacityFrom: 0.4,
          opacityTo: 0.05,
          stops: [0, 100]
        }
      },
      stroke: {
        curve: 'smooth',
        width: 3
      },
      dataLabels: { enabled: false },
      markers: {
        size: 4,
        colors: ['#6366f1'],
        strokeColors: '#fff',
        strokeWidth: 2,
        hover: { size: 6 }
      },
      xaxis: {
        categories,
        labels: {
          style: { colors: '#94a3b8', fontSize: '10px' },
          rotate: -45,
          rotateAlways: false
        },
        axisBorder: { show: false },
        axisTicks: { show: false }
      },
      yaxis: {
        min: 0,
        max: 100,
        labels: {
          style: { colors: '#94a3b8', fontSize: '11px' },
          formatter: (val: number) => val + '%'
        }
      },
      grid: {
        borderColor: '#f1f5f9',
        strokeDashArray: 4,
        xaxis: { lines: { show: false } },
        yaxis: { lines: { show: true } }
      },
      tooltip: {
        theme: 'light',
        y: {
          formatter: (val: number) => val.toFixed(1) + '%'
        }
      },
      annotations: {
        yaxis: [{
          y: 85,
          borderColor: '#22c55e',
          strokeDashArray: 4,
          label: {
            text: 'Target 85%',
            position: 'left',
            style: {
              color: '#22c55e',
              background: 'transparent',
              fontSize: '10px'
            }
          }
        }]
      },
      legend: { show: false }
    };
  }
}
