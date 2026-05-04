import { Component, Input, OnInit, OnChanges, SimpleChanges, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  NgApexchartsModule, ChartComponent,
  ApexChart, ApexAxisChartSeries, ApexXAxis, ApexDataLabels,
  ApexStroke, ApexGrid, ApexTooltip, ApexFill, ApexLegend, ApexYAxis
} from 'ng-apexcharts';
import { ExecutionTrend } from '../../models/dashboard.model';

export type ChartOptions = {
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
};

@Component({
  selector: 'app-execution-trends-chart',
  standalone: true,
  imports: [CommonModule, NgApexchartsModule],
  templateUrl: './execution-trends-chart.component.html',
  styleUrl: './execution-trends-chart.component.scss'
})
export class ExecutionTrendsChartComponent implements OnInit, OnChanges {
  @ViewChild("chart") chartRef!: ChartComponent;
  @Input() trends: ExecutionTrend[] = [];
  
  public chartOptions: Partial<ChartOptions> | null = null;

  ngOnInit() {
    if (this.trends && this.trends.length > 0) {
      this.initializeChart();
    }
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['trends'] && this.trends && this.trends.length > 0) {
      // Always reinitialize — ApexCharts ViewChild is unreliable with *ngIf
      this.initializeChart();
    }
  }

  private initializeChart() {
    const categories = this.trends.map(t => t.date);
    const passedData = this.trends.map(t => t.passed);
    const failedData = this.trends.map(t => t.failed);

    this.chartOptions = {
      series: [
        {
          name: "Passed",
          data: passedData
        },
        {
          name: "Failed",
          data: failedData
        }
      ],
      chart: {
        height: 320,
        type: "bar",
        stacked: true,
        toolbar: {
          show: false
        },
        fontFamily: 'Inter, sans-serif',
        animations: {
          enabled: true,
          easing: 'easeinout',
          speed: 800
        }
      },
      colors: ['#22c55e', '#ef4444'], // Green for passed, Red for failed
      fill: {
        type: 'gradient',
        gradient: {
          shade: 'light',
          type: 'vertical',
          shadeIntensity: 0.25,
          opacityFrom: 0.9,
          opacityTo: 0.7,
          stops: [0, 100]
        }
      },
      dataLabels: {
        enabled: false
      },
      stroke: {
        show: true,
        width: 1,
        colors: ['transparent']
      },
      xaxis: {
        categories: categories,
        labels: {
          style: {
            colors: '#64748b',
            fontSize: '11px',
            fontWeight: 600
          },
          rotate: -45,
          rotateAlways: false
        },
        axisBorder: {
          show: false
        },
        axisTicks: {
          show: false
        }
      },
      yaxis: {
        title: {
          text: 'Test Executions',
          style: {
            color: '#64748b',
            fontSize: '12px',
            fontWeight: 600
          }
        },
        labels: {
          style: {
            colors: '#64748b',
            fontSize: '11px'
          }
        }
      },
      grid: {
        show: true,
        borderColor: '#f1f5f9',
        strokeDashArray: 4,
        position: 'back',
        xaxis: {
          lines: {
            show: false
          }
        },
        yaxis: {
          lines: {
            show: true
          }
        }
      },
      tooltip: {
        theme: 'light',
        shared: true,
        intersect: false,
        y: {
          formatter: function (val: number) {
            return val + " tests";
          }
        }
      },
      legend: {
        show: true,
        position: 'top',
        horizontalAlign: 'right',
        markers: {
          width: 8,
          height: 8,
          strokeWidth: 0
        },
        itemMargin: {
          horizontal: 12
        },
        labels: {
          colors: '#64748b'
        }
      }
    };
  }
}

