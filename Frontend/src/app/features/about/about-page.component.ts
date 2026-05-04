import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-about-page',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './about-page.component.html'
})
export class AboutPageComponent {
  readonly principles: string[] = [
    'Clarity first: every test should be readable by product and engineering teams.',
    'Reliable automation: reduce flaky behavior with stable structure and reusable assets.',
    'Role-safe collaboration: each role gets the right power level for secure teamwork.',
    'Traceability by design: every critical change can be audited and explained.'
  ];

  readonly capabilities: string[] = [
    'BDD scenario management with clean Given/When/Then flows',
    'Suite building and execution tracking with detailed test run insights',
    'NLP action mappings to connect language and automation behavior',
    'Audit logs and access control for governance and accountability'
  ];
}
