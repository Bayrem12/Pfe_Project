import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RecentActivity } from '../../models/dashboard.model';
import { formatDistanceToNow } from 'date-fns';

@Component({
  selector: 'app-recent-activity',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './recent-activity.component.html',
  styleUrl: './recent-activity.component.scss'
})
export class RecentActivityComponent {
  @Input() activities: RecentActivity[] = [];

  getActivityColor(type: string): string {
    switch (type) {
      case 'success': return 'bg-tertiary-fixed-variant shadow-[0_0_8px_rgba(111,251,190,0.8)]';
      case 'error': return 'bg-error';
      case 'warning': return 'bg-secondary';
      default: return 'bg-slate-200';
    }
  }

  formatTime(date: Date): string {
    return formatDistanceToNow(new Date(date), { addSuffix: true });
  }
}

