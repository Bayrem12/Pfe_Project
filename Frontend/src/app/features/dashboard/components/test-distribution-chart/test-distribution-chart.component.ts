import { Component, Input, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgApexchartsModule, ChartComponent, ApexChart, ApexNonAxisChartSeries, ApexLegend, ApexDataLabels, ApexPlotOptions, ApexStroke } from 'ng-apexcharts';
import { TestDistribution } from '../../models/dashboard.model';

export type DonutChartOptions = {
  series: ApexNonAxisChartSeries;
  chart: ApexChart;
  labels: string[];
  colors: string[];
  legend: ApexLegend;
  dataLabels: ApexDataLabels;
  plotOptions: ApexPlotOptions;
  stroke: ApexStroke;
};

@Component({
  selector: 'app-test-distribution-chart',
  standalone: true,
  imports: [CommonModule, NgApexchartsModule],
  templateUrl: './test-distribution-chart.component.html',
  styleUrl: './test-distribution-chart.component.scss'
})
export class TestDistributionChartComponent implements OnInit {
  @ViewChild("chart") chart!: ChartComponent;
  @Input() distribution!: TestDistribution;
  
  public chartOptions!: Partial<DonutChartOptions>;

  ngOnInit() {
    this.initializeChart();
  }

  ngOnChanges() {
    if (this.distribution) {
      this.initializeChart();
    }
  }

  private initializeChart() {
    if (!this.distribution) return;

    this.chartOptions = {
      series: [this.distribution.passed, this.distribution.failed, this.distribution.skipped || 0],
      chart: {
        type: "donut",
        height: 220,
        fontFamily: 'Inter, sans-serif'
      },
      labels: ["Passed", "Failed", "Skipped"],
      colors: ['#1e40af', '#ffdad6', '#e2e8f0'],
      dataLabels: {
        enabled: false
      },
      legend: {
        show: false
      },
      plotOptions: {
        pie: {
          donut: {
            size: '70%',
            labels: {
              show: true,
              name: {
                show: false
              },
              value: {
                show: true,
                fontSize: '24px',
                fontWeight: 900,
                color: '#111c2d',
                formatter: function (val: string) {
                  return val;
                }
              },
              total: {
                show: true,
                label: 'Total',
                fontSize: '10px',
                fontWeight: 700,
                color: '#94a3b8',
                formatter: function (w: { globals: { seriesTotals: number[] } }) {
                  const total = w.globals.seriesTotals.reduce((a: number, b: number) => a + b, 0);
                  return total.toLocaleString();
                }
              }
            }
          }
        }
      },
      stroke: {
        width: 0
      }
    };
  }
}

