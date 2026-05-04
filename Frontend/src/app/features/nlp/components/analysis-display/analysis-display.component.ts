import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StepAnalysis } from '../../models/nlp.model';

@Component({
  selector: 'app-analysis-display',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './analysis-display.component.html',
  styleUrl: './analysis-display.component.scss'
})
export class AnalysisDisplayComponent implements OnInit {
  @Input() analyses: StepAnalysis[] = [];
  @Input() scenarioId?: string;
  @Input() showHeader = true;

  // Expose Object for template
  Object = Object;

  // Stats
  totalSteps = 0;
  averageConfidence = 0;
  intentDistribution: { [key: string]: number } = {};

  ngOnInit(): void {
    this.calculateStats();
  }

  ngOnChanges(): void {
    this.calculateStats();
  }

  private calculateStats(): void {
    this.totalSteps = this.analyses.length;
    
    if (this.totalSteps > 0) {
      // Calculate average confidence
      const totalConfidence = this.analyses.reduce((sum, a) => sum + a.confidence, 0);
      this.averageConfidence = totalConfidence / this.totalSteps;

      // Calculate intent distribution
      this.intentDistribution = {};
      this.analyses.forEach(analysis => {
        this.intentDistribution[analysis.intent] = 
          (this.intentDistribution[analysis.intent] || 0) + 1;
      });
    }
  }

  getIntentIcon(intent: string): string {
    const icons: { [key: string]: string } = {
      'Navigate': 'open_in_browser',
      'Click': 'mouse',
      'Input': 'keyboard',
      'Type': 'keyboard',
      'Select': 'list',
      'Assert': 'check_circle',
      'Verify': 'verified',
      'Wait': 'hourglass_empty',
      'Hover': 'pan_tool',
      'Scroll': 'swap_vert',
      'Custom': 'code'
    };
    return icons[intent] || 'code';
  }

  getIntentColor(intent: string): string {
    const colors: { [key: string]: string } = {
      'Navigate': 'bg-tertiary-container/10 text-on-tertiary-container border-tertiary-container/20',
      'Click': 'bg-primary-container/10 text-primary-container border-primary-container/20',
      'Input': 'bg-secondary-container/10 text-secondary-container border-secondary-container/20',
      'Type': 'bg-secondary-container/10 text-secondary-container border-secondary-container/20',
      'Select': 'bg-primary/10 text-primary border-primary/20',
      'Assert': 'bg-tertiary-fixed/20 text-on-tertiary-fixed border-tertiary-fixed/30',
      'Verify': 'bg-tertiary-fixed/20 text-on-tertiary-fixed border-tertiary-fixed/30',
      'Wait': 'bg-error-container/20 text-on-error-container border-error/20',
      'Hover': 'bg-outline/10 text-on-surface-variant border-outline/20',
      'Scroll': 'bg-surface-variant/50 text-on-surface-variant border-outline-variant/30',
      'Custom': 'bg-surface-container text-on-surface border-outline-variant'
    };
    return colors[intent] || 'bg-surface-container text-on-surface border-outline-variant';
  }

  getConfidenceColor(confidence: number): string {
    if (confidence >= 0.9) return 'text-tertiary-fixed-dim';
    if (confidence >= 0.7) return 'text-primary';
    if (confidence >= 0.5) return 'text-secondary';
    return 'text-error';
  }

  getConfidenceLabel(confidence: number): string {
    if (confidence >= 0.9) return 'Excellent';
    if (confidence >= 0.7) return 'Good';
    if (confidence >= 0.5) return 'Fair';
    return 'Low';
  }

  getConfidenceBarWidth(confidence: number): string {
    return `${confidence * 100}%`;
  }

  getConfidenceBarColor(confidence: number): string {
    if (confidence >= 0.9) return 'bg-tertiary-fixed-dim';
    if (confidence >= 0.7) return 'bg-primary';
    if (confidence >= 0.5) return 'bg-secondary';
    return 'bg-error';
  }

  getIntentDistributionPercentage(count: number): number {
    return this.totalSteps > 0 ? (count / this.totalSteps) * 100 : 0;
  }

  exportAnalysis(): void {
    const data = JSON.stringify(this.analyses, null, 2);
    const blob = new Blob([data], { type: 'application/json' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `step-analysis-${this.scenarioId || 'export'}.json`;
    a.click();
    window.URL.revokeObjectURL(url);
  }
}
