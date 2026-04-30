import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NlpService } from '../../services/nlp.service';
import { ParseGherkinResponse, ParsedStep, GHERKIN_KEYWORDS } from '../../models/nlp.model';

@Component({
  selector: 'app-gherkin-parser',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './gherkin-parser.component.html',
  styleUrl: './gherkin-parser.component.scss'
})
export class GherkinParserComponent {
  private nlpService = inject(NlpService);

  // Input text - start empty
  gherkinInput = '';

  // Parse results
  parseResult: ParseGherkinResponse | null = null;
  isLoading = false;
  errorMessage = '';

  // Gherkin keyword styles
  keywordStyles = GHERKIN_KEYWORDS;

  parseGherkin(): void {
    if (!this.gherkinInput.trim()) {
      this.errorMessage = 'Please enter Gherkin text';
      return;
    }

    this.isLoading = true;
    this.errorMessage = '';
    this.parseResult = null;

    this.nlpService.parseGherkin(this.gherkinInput).subscribe({
      next: (result) => {
        this.parseResult = result;
        this.isLoading = false;
      },
      error: (error) => {
        console.error('Parse error:', error);
        this.errorMessage = 'Failed to parse Gherkin text. Please check the format.';
        this.isLoading = false;
      }
    });
  }

  clearInput(): void {
    this.gherkinInput = '';
    this.parseResult = null;
    this.errorMessage = '';
  }

  loadSample(sampleType: 'login' | 'checkout' | 'registration'): void {
    const samples = {
      login: `Feature: User Login
Scenario: Valid user login
  Given I am on the login page
  When I enter username "john@example.com"
  And I enter password "secret123"
  And I click the login button
  Then I should see the dashboard`,
      
      checkout: `Feature: Shopping Cart Checkout
Scenario: Complete purchase flow
  Given I have items in my cart
  When I proceed to checkout
  And I enter shipping address
  And I select payment method "Credit Card"
  And I click place order
  Then I should see order confirmation
  And I should receive confirmation email`,
      
      registration: `Feature: User Registration
Scenario: New user signs up
  Given I am on the registration page
  When I enter email "newuser@test.com"
  And I enter password "Pass123!"
  And I enter password confirmation "Pass123!"
  And I agree to terms and conditions
  And I click register button
  Then I should receive verification email
  And I should be redirected to welcome page`
    };

    this.gherkinInput = samples[sampleType];
    this.parseGherkin();
  }

  getKeywordStyle(keyword: string): { color: string; bgColor: string } {
    const style = this.keywordStyles.find(k => k.keyword === keyword);
    return style || { color: '#64748b', bgColor: '#f1f5f9' };
  }

  copyToClipboard(): void {
    navigator.clipboard.writeText(this.gherkinInput).then(() => {
      // Could add a toast notification here
      console.log('Copied to clipboard');
    });
  }

  downloadResults(): void {
    if (!this.parseResult) return;

    const data = JSON.stringify(this.parseResult, null, 2);
    const blob = new Blob([data], { type: 'application/json' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'gherkin-parse-results.json';
    a.click();
    window.URL.revokeObjectURL(url);
  }
}
