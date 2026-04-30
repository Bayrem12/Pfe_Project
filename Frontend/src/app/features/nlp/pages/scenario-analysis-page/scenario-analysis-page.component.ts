import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { NlpService } from '../../services/nlp.service';
import { AnalysisDisplayComponent } from '../../components/analysis-display/analysis-display.component';
import { StepAnalysis } from '../../models/nlp.model';

@Component({
  selector: 'app-scenario-analysis-page',
  standalone: true,
  imports: [CommonModule, AnalysisDisplayComponent],
  templateUrl: './scenario-analysis-page.component.html',
  styleUrl: './scenario-analysis-page.component.scss'
})
export class ScenarioAnalysisPageComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private nlpService = inject(NlpService);

  scenarioId?: string;
  analyses: StepAnalysis[] = [];
  isLoading = false;
  errorMessage = '';

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      this.scenarioId = params.get('id') || undefined;
      if (this.scenarioId) {
        this.loadAnalysis();
      }
    });
  }

  loadAnalysis(): void {
    if (!this.scenarioId) return;

    this.isLoading = true;
    this.errorMessage = '';

    this.nlpService.analyzeScenario(this.scenarioId).subscribe({
      next: (analyses) => {
        this.analyses = analyses;
        this.isLoading = false;
      },
      error: (error) => {
        console.error('Analysis error:', error);
        this.errorMessage = 'Failed to load analysis. Please ensure the scenario exists and has steps.';
        this.isLoading = false;
        this.analyses = [];
      }
    });
  }

  retryAnalysis(): void {
    this.loadAnalysis();
  }
}
