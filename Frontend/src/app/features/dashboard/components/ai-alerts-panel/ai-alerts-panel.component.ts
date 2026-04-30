import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AIAlert, GlobalStatus } from '../../models/dashboard.model';

@Component({
  selector: 'app-ai-alerts-panel',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './ai-alerts-panel.component.html',
  styleUrl: './ai-alerts-panel.component.scss'
})
export class AiAlertsPanelComponent {
  @Input() alerts: AIAlert[] = [];
  @Input() globalStatus!: GlobalStatus;
  @Output() alertAction = new EventEmitter<{ alert: AIAlert }>();

  onAlertAction(alert: AIAlert) {
    this.alertAction.emit({ alert });
  }

  getAlertClass(severity: string): string {
    switch (severity) {
      case 'critical': return 'bg-error-container/40 border-l-4 border-error';
      case 'warning': return 'bg-surface-container border border-outline-variant/30';
      default: return 'bg-gradient-to-br from-primary-container/5 to-secondary/5 border border-primary-fixed-dim/20';
    }
  }

  getAlertButtonClass(severity: string): string {
    switch (severity) {
      case 'critical': return 'text-error underline decoration-2 underline-offset-4';
      case 'warning': return 'text-primary';
      default: return 'text-primary';
    }
  }
}

