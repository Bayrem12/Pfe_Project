import { Component, OnInit, inject, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { NlpService } from '../../services/nlp.service';
import { ProjectService } from '../../../../core/services/project.service';
import { AuthService } from '../../../../core/services/auth.service';
import { Project } from '../../../../core/models/project.model';
import { 
  ActionMapping, 
  CreateActionMappingRequest,
  ACTION_TYPE_OPTIONS,
  SELECTOR_STRATEGY_OPTIONS,
  ParsedStep
} from '../../models/nlp.model';

// Local interface for our analysis display
interface AnalysisResult {
  stepId: string;
  stepText: string;
  detectedIntent: string;
  confidenceScore: number;
  parameters: { name: string; value: string }[];
  suggestedAction: string;
}

type TabType = 'mappings' | 'parser' | 'analysis';

@Component({
  selector: 'app-nlp-action-mappings-page',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './nlp-action-mappings-page.component.html',
  styleUrl: './nlp-action-mappings-page.component.scss'
})
export class NlpActionMappingsPageComponent implements OnInit {
  private nlpService = inject(NlpService);
  private projectService = inject(ProjectService);
  private authService = inject(AuthService);
  private fb = inject(FormBuilder);

  // UI State
  isLoading = false;
  isLoadingProjects = false;
  showCreateModal = false;
  editingMapping: ActionMapping | null = null;
  errorMessage = '';
  activeTab: TabType = 'mappings';
  projectDropdownOpen = false;

  @HostListener('document:click')
  onDocumentClick(): void {
    this.projectDropdownOpen = false;
  }

  // Data
  mappings: ActionMapping[] = [];
  projects: Project[] = [];
  selectedProjectId = '';
  
  // Options for dropdowns
  actionTypes = ACTION_TYPE_OPTIONS;
  selectorStrategies = SELECTOR_STRATEGY_OPTIONS;

  // Form
  mappingForm!: FormGroup;

  // Gherkin Parser State
  gherkinInput = '';
  isParsing = false;
  parsedSteps: ParsedStep[] = [];
  parseError = '';

  // Analysis State
  isAnalyzing = false;
  analysisResults: AnalysisResult[] = [];
  analysisError = '';

  // Sample Gherkin templates
  gherkinSamples = {
    login: `Feature: User Login
  Scenario: Successful login with valid credentials
    Given I am on the login page
    When I enter username "testuser"
    And I enter password "secret123"
    And I click the login button
    Then I should see the dashboard
    And I should see welcome message "Hello, testuser"`,
    
    checkout: `Feature: E-commerce Checkout
  Scenario: Complete purchase flow
    Given I have items in my shopping cart
    When I proceed to checkout
    And I enter shipping address "123 Main St"
    And I select payment method "Credit Card"
    And I enter card number "4111111111111111"
    Then I should see order confirmation
    And I should receive confirmation email`,
    
    registration: `Feature: User Registration
  Scenario: New user signs up successfully
    Given I am on the registration page
    When I enter my email "newuser@example.com"
    And I enter my password "SecurePass123!"
    And I confirm my password "SecurePass123!"
    And I accept the terms and conditions
    And I click the register button
    Then I should see a success message
    And I should be redirected to the welcome page`
  };

  get isViewerRole(): boolean {
    return this.authService.hasRole('Viewer');
  }

  get canManageMappings(): boolean {
    return this.authService.canManageNlpMappings();
  }

  get canParseGherkinText(): boolean {
    return this.authService.canParseGherkinText();
  }

  ngOnInit(): void {
    this.initForm();
    if (this.isViewerRole) {
      this.activeTab = 'parser';
    }
    this.loadProjects();
  }

  private denyMappingWriteAction(actionLabel: string): boolean {
    if (this.canManageMappings) {
      return false;
    }

    this.errorMessage = `Viewer role is read-only and cannot ${actionLabel} NLP mappings. You can still parse Gherkin text.`;
    return true;
  }

  private initForm(): void {
    this.mappingForm = this.fb.group({
      intentPattern: ['', [Validators.required, Validators.minLength(3)]],
      actionType: ['Click', Validators.required],
      selectorStrategy: ['css', Validators.required],
      selectorValue: ['', [Validators.required, Validators.minLength(1)]],
      description: ['', [Validators.required, Validators.minLength(5)]],
      priority: [0, [Validators.required, Validators.min(0), Validators.max(100)]]
    });
  }

  loadProjects(): void {
    this.isLoadingProjects = true;
    this.errorMessage = '';
    
    this.projectService.getUserProjects().subscribe({
      next: (projects) => {
        this.projects = projects;
        this.isLoadingProjects = false;
        
        if (projects.length > 0 && !this.selectedProjectId) {
          this.selectedProjectId = projects[0].id;
          this.loadMappings();
        } else if (projects.length === 0) {
          this.errorMessage = 'No projects found. Please create a project first or check that you have access.';
        }
      },
      error: (error) => {
        console.error('Error loading projects:', error);
        this.errorMessage = `Failed to load projects: ${error.message || 'Unknown error'}`;
        this.isLoadingProjects = false;
      }
    });
  }

  loadMappings(): void {
    if (!this.selectedProjectId) return;
    
    this.isLoading = true;
    this.errorMessage = '';
    
    this.nlpService.getActionMappings(this.selectedProjectId).subscribe({
      next: (mappings) => {
        this.mappings = mappings;
        this.isLoading = false;
      },
      error: (error) => {
        console.error('Error loading mappings:', error);
        this.mappings = [];
        this.isLoading = false;
      }
    });
  }

  onProjectChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    this.selectedProjectId = select.value;
    this.loadMappings();
  }

  selectProject(projectId: string): void {
    this.selectedProjectId = projectId;
    this.projectDropdownOpen = false;
    this.loadMappings();
  }

  setActiveTab(tab: TabType): void {
    this.activeTab = tab;
  }

  openCreateModal(): void {
    if (this.denyMappingWriteAction('create')) return;

    this.editingMapping = null;
    this.mappingForm.reset({
      intentPattern: '',
      actionType: 'Click',
      selectorStrategy: 'css',
      selectorValue: '',
      description: '',
      priority: 0
    });
    this.showCreateModal = true;
  }

  openEditModal(mapping: ActionMapping): void {
    if (this.denyMappingWriteAction('edit')) return;

    this.editingMapping = mapping;
    this.mappingForm.patchValue({
      intentPattern: mapping.intentPattern,
      actionType: mapping.actionType,
      selectorStrategy: mapping.selectorStrategy,
      selectorValue: mapping.selectorValue,
      description: mapping.description,
      priority: mapping.priority ?? 0
    });
    this.showCreateModal = true;
  }

  closeModal(): void {
    this.showCreateModal = false;
    this.editingMapping = null;
    this.mappingForm.reset();
  }

  saveMapping(): void {
    if (this.denyMappingWriteAction('save')) return;

    if (this.mappingForm.invalid) return;

    const request: CreateActionMappingRequest = this.mappingForm.value;

    if (this.editingMapping) {
      this.nlpService.updateActionMapping(this.editingMapping.id, request).subscribe({
        next: (updated) => {
          const index = this.mappings.findIndex(m => m.id === this.editingMapping!.id);
          if (index !== -1) {
            this.mappings[index] = updated;
          }
          this.closeModal();
        },
        error: (error) => {
          console.error('Error updating mapping:', error);
          this.errorMessage = 'Failed to update mapping. Please try again.';
        }
      });
    } else {
      this.nlpService.createActionMapping(this.selectedProjectId, request).subscribe({
        next: (newMapping) => {
          this.mappings.unshift(newMapping);
          this.closeModal();
        },
        error: (error) => {
          console.error('Error creating mapping:', error);
          this.errorMessage = 'Failed to create mapping. Please try again.';
        }
      });
    }
  }

  deleteMapping(mapping: ActionMapping): void {
    if (this.denyMappingWriteAction('delete')) return;

    if (!confirm(`Delete mapping "${mapping.intentPattern}"?`)) return;

    this.nlpService.deleteActionMapping(mapping.id).subscribe({
      next: () => {
        this.mappings = this.mappings.filter(m => m.id !== mapping.id);
      },
      error: (error) => {
        console.error('Error deleting mapping:', error);
        this.errorMessage = 'Failed to delete mapping. Please try again.';
      }
    });
  }

  toggleStatus(mapping: ActionMapping): void {
    if (this.denyMappingWriteAction('update')) return;

    const newStatus = !mapping.isActive;
    this.nlpService.toggleActionMappingStatus(mapping.id, newStatus).subscribe({
      next: (updated) => {
        mapping.isActive = updated.isActive;
      },
      error: (error) => {
        console.error('Error toggling status:', error);
        this.errorMessage = 'Failed to update status. Please try again.';
      }
    });
  }

  // Parser Methods
  parseGherkin(): void {
    if (!this.canParseGherkinText) {
      this.parseError = 'Your role cannot parse Gherkin content.';
      return;
    }

    if (!this.gherkinInput.trim()) {
      this.parseError = 'Please enter Gherkin content to parse';
      return;
    }

    this.isParsing = true;
    this.parseError = '';
    this.parsedSteps = [];

    this.nlpService.parseGherkin(this.gherkinInput).subscribe({
      next: (response) => {
        this.parsedSteps = response.steps || [];
        this.isParsing = false;
        
        if (this.parsedSteps.length === 0) {
          this.parseError = 'No steps found in the provided Gherkin content';
        }
      },
      error: (error) => {
        console.error('Parse error:', error);
        this.parseError = 'Failed to parse Gherkin content. Please check the syntax.';
        this.isParsing = false;
      }
    });
  }

  loadSample(type: 'login' | 'checkout' | 'registration'): void {
    this.gherkinInput = this.gherkinSamples[type];
    this.parsedSteps = [];
    this.parseError = '';
  }

  clearParser(): void {
    this.gherkinInput = '';
    this.parsedSteps = [];
    this.parseError = '';
  }

  analyzeSteps(): void {
    if (this.parsedSteps.length === 0) {
      this.analysisError = 'Please parse some Gherkin steps first';
      return;
    }

    this.isAnalyzing = true;
    this.analysisError = '';
    this.analysisResults = [];

    // Simulate analysis based on parsed steps
    setTimeout(() => {
      this.analysisResults = this.parsedSteps.map((step, index) => ({
        stepId: `step-${index}`,
        stepText: step.text,
        detectedIntent: this.detectIntent(step.text),
        confidenceScore: 0.75 + Math.random() * 0.24,
        parameters: this.extractParameters(step.text),
        suggestedAction: this.suggestAction(step.text)
      }));
      this.isAnalyzing = false;
      this.activeTab = 'analysis';
    }, 800);
  }

  private detectIntent(text: string): string {
    const lowerText = text.toLowerCase();
    if (lowerText.includes('click') || lowerText.includes('press')) return 'Click';
    if (lowerText.includes('enter') || lowerText.includes('type') || lowerText.includes('input')) return 'Input';
    if (lowerText.includes('navigate') || lowerText.includes('go to') || lowerText.includes('open')) return 'Navigate';
    if (lowerText.includes('select') || lowerText.includes('choose')) return 'Select';
    if (lowerText.includes('see') || lowerText.includes('should') || lowerText.includes('verify')) return 'Assert';
    if (lowerText.includes('wait') || lowerText.includes('pause')) return 'Wait';
    return 'Custom';
  }

  private extractParameters(text: string): { name: string; value: string }[] {
    const params: { name: string; value: string }[] = [];
    const matches = text.match(/"([^"]+)"/g);
    if (matches) {
      matches.forEach((match, index) => {
        params.push({
          name: `param${index + 1}`,
          value: match.replace(/"/g, '')
        });
      });
    }
    return params;
  }

  private suggestAction(text: string): string {
    const intent = this.detectIntent(text);
    switch (intent) {
      case 'Click': return 'click(selector)';
      case 'Input': return 'type(selector, value)';
      case 'Navigate': return 'goto(url)';
      case 'Select': return 'select(selector, option)';
      case 'Assert': return 'expect(selector).toBeVisible()';
      case 'Wait': return 'wait(duration)';
      default: return 'custom(args)';
    }
  }

  getKeywordClass(keyword: string): string {
    const classes: { [key: string]: string } = {
      'Given': 'bg-blue-500/20 text-blue-700 border-blue-500/30',
      'When': 'bg-amber-500/20 text-amber-700 border-amber-500/30',
      'Then': 'bg-green-500/20 text-green-700 border-green-500/30',
      'And': 'bg-purple-500/20 text-purple-700 border-purple-500/30',
      'But': 'bg-red-500/20 text-red-700 border-red-500/30'
    };
    return classes[keyword] || 'bg-gray-500/20 text-gray-700 border-gray-500/30';
  }

  getConfidenceClass(score: number): string {
    if (score >= 0.9) return 'text-emerald-600';
    if (score >= 0.7) return 'text-amber-600';
    return 'text-red-600';
  }

  getActionTypeColor(actionType: string): string {
    const colors: { [key: string]: string } = {
      'Click': 'bg-blue-500/10 text-blue-700 border-blue-500/20',
      'Type': 'bg-purple-500/10 text-purple-700 border-purple-500/20',
      'Navigate': 'bg-teal-500/10 text-teal-700 border-teal-500/20',
      'Select': 'bg-indigo-500/10 text-indigo-700 border-indigo-500/20',
      'Assert': 'bg-green-500/10 text-green-700 border-green-500/20',
      'Hover': 'bg-orange-500/10 text-orange-700 border-orange-500/20',
      'Scroll': 'bg-cyan-500/10 text-cyan-700 border-cyan-500/20',
      'Wait': 'bg-yellow-500/10 text-yellow-700 border-yellow-500/20'
    };
    return colors[actionType] || 'bg-gray-500/10 text-gray-700 border-gray-500/20';
  }

  getSelectedProjectName(): string {
    return this.projects.find(p => p.id === this.selectedProjectId)?.name || 'Select a project';
  }

  getActiveMappingsCount(): number {
    return this.mappings.filter(m => m.isActive).length;
  }

  getAverageConfidence(): string {
    if (this.analysisResults.length === 0) return '0%';
    const avg = this.analysisResults.reduce((sum, r) => sum + r.confidenceScore, 0) / this.analysisResults.length;
    return `${Math.round(avg * 100)}%`;
  }

  getIntentCounts(): { [key: string]: number } {
    const counts: { [key: string]: number } = {};
    this.analysisResults.forEach(r => {
      counts[r.detectedIntent] = (counts[r.detectedIntent] || 0) + 1;
    });
    return counts;
  }

  exportAnalysis(): void {
    const data = JSON.stringify(this.analysisResults, null, 2);
    const blob = new Blob([data], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'nlp-analysis.json';
    a.click();
    URL.revokeObjectURL(url);
  }
}
