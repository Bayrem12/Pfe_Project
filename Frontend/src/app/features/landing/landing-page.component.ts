import { Component, OnInit, AfterViewInit, HostListener, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

@Component({
  selector: 'app-landing-page',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  templateUrl: './landing-page.component.html',
  styleUrls: ['./landing-page.component.scss'],
})
export class LandingPageComponent implements OnInit, AfterViewInit {
  private router = inject(Router);

  emailInput = '';
  isScrolled = false;
  mobileMenuOpen = false;
  activeSection = 'features';

  features = [
    {
      icon: 'edit_note',
      color: 'primary',
      title: 'BDD Scenario Editor',
      description: 'Write Gherkin scenarios with Given / When / Then. Organize tests by project, module, and feature with real-time syntax validation.',
      badge: 'Gherkin',
      badgeColor: 'primary',
    },
    {
      icon: 'psychology',
      color: 'secondary',
      title: 'AI Quality Analyzer',
      description: 'Score your scenarios 0–100 before running. Get actionable suggestions powered by rule-based analysis and Mistral LLM.',
      badge: 'Mistral LLM',
      badgeColor: 'secondary',
    },
    {
      icon: 'account_tree',
      color: 'tertiary',
      title: 'NLP Action Mappings',
      description: 'Map natural language steps to precise Playwright actions. Configure selectors and parameters per project.',
      badge: 'NLP Engine',
      badgeColor: 'tertiary',
    },
    {
      icon: 'play_circle',
      color: 'primary',
      title: 'AI Test Execution',
      description: 'Run Gherkin scenarios end-to-end with Playwright. Screenshots captured per step, HTML reports generated automatically.',
      badge: 'Playwright',
      badgeColor: 'primary',
    },
    {
      icon: 'history',
      color: 'secondary',
      title: 'Execution History',
      description: 'Full audit trail of every test run — step results, screenshots, failure analysis, and AI-powered re-test recommendations.',
      badge: 'Analytics',
      badgeColor: 'secondary',
    },
    {
      icon: 'manage_accounts',
      color: 'tertiary',
      title: 'Project Management',
      description: 'Role-based access control (Admin, Manager, Tester, Viewer). Organize test assets across multiple projects with per-project settings.',
      badge: 'RBAC',
      badgeColor: 'tertiary',
    },
    {
      icon: 'folder_special',
      color: 'primary',
      title: 'Test Suites',
      description: 'Group and manage scenarios into test suites. Build targeted regression packs and execute them with a single click.',
      badge: 'Organized',
      badgeColor: 'primary',
    },
    {
      icon: 'bar_chart',
      color: 'secondary',
      title: 'Dashboard & Analytics',
      description: 'Visual overview of test runs, pass rates, quality trends, and team activity — all in one real-time executive dashboard.',
      badge: 'Real-time',
      badgeColor: 'secondary',
    },
  ];

  howItWorksSteps = [
    {
      icon: 'edit_note',
      color: 'primary',
      step: '01',
      title: 'Write',
      description: 'Author Gherkin BDD scenarios in the editor. Organize by project → module → feature. Add tags and link to test suites.',
    },
    {
      icon: 'psychology',
      color: 'secondary',
      step: '02',
      title: 'Analyze',
      description: 'AI scores your scenario quality (0–100) and Mistral LLM suggests improvements — before you run a single test.',
    },
    {
      icon: 'play_arrow',
      color: 'tertiary',
      step: '03',
      title: 'Execute',
      description: 'Launch AI-powered Playwright execution. Get step-by-step results, screenshots per step, failure analysis, and HTML reports instantly.',
    },
  ];

  @HostListener('window:scroll')
  onScroll() {
    this.isScrolled = window.scrollY > 20;
  }

  ngOnInit(): void {}

  ngAfterViewInit(): void {
    // Intersection Observer for scroll animations
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add('animate-visible');
          }
        });
      },
      { threshold: 0.1 }
    );

    document.querySelectorAll('.animate-on-scroll').forEach((el) => observer.observe(el));
  }

  navigateTo(route: string) {
    this.router.navigate([route]);
  }

  scrollToSection(sectionId: string) {
    const el = document.getElementById(sectionId);
    if (el) el.scrollIntoView({ behavior: 'smooth' });
    this.mobileMenuOpen = false;
  }

  submitEmail() {
    if (this.emailInput) {
      this.router.navigate(['/auth/register'], { queryParams: { email: this.emailInput } });
    }
  }
}
