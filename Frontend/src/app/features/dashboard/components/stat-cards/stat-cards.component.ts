import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DashboardSummary } from '../../models/dashboard.model';

@Component({
  selector: 'app-stat-cards',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './stat-cards.component.html',
  styleUrl: './stat-cards.component.scss'
})
export class StatCardsComponent {
  @Input() summary!: DashboardSummary;
  
  getCircleOffset(percentage: number): number {
    const circumference = 2 * Math.PI * 28;
    return circumference - (percentage / 100) * circumference;
  }
}
