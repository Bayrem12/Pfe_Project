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

  stats = [
    { value: '500+', label: 'Engineering Teams' },
    { value: '3x', label: 'Faster Releases' },
    { value: '99.9%', label: 'Test Reliability' },
    { value: '40%', label: 'Less Maintenance' },
  ];

  aiFeatures = [
    {
      icon: 'cycle',
      color: 'secondary',
      title: 'Smart Re-test Recommendations',
      description:
        'Automatically identifies and re-runs only the affected modules after code changes — reducing build time dramatically.',
      badge: 'ML-Powered',
      badgeColor: 'secondary',
      progress: 75,
    },
    {
      icon: 'content_copy',
      color: 'primary',
      title: 'Duplicate Detection',
      description:
        'Eliminate redundancy. Our LLM analyzes test intent to prevent duplicate scripts across teams using semantic similarity.',
      badge: 'LLM Engine',
      badgeColor: 'primary',
      wide: true,
    },
    {
      icon: 'analytics',
      color: 'tertiary',
      title: 'Quality Scoring',
      description:
        'Real-time health monitoring of your entire test suite with actionable improvement scores.',
      badge: 'Real-time',
      badgeColor: 'tertiary',
      wide: true,
    },
    {
      icon: 'timeline',
      color: 'secondary',
      title: 'Execution Prediction',
      description:
        "Know if your build will pass before you hit 'Run' based on deep historical analysis.",
      badge: 'Deep ML',
      badgeColor: 'secondary',
    },
    {
      icon: 'auto_fix_high',
      color: 'primary',
      title: 'Coverage Analysis',
      description:
        'Identify blind spots in your test coverage and get AI suggestions for new scenarios to maximize confidence.',
      badge: 'AI Insights',
      badgeColor: 'primary',
    },
  ];

  comparisonRows = [
    { feature: 'Test Authoring', legacy: 'Script-heavy coding', autotestify: 'NLP & AI-Generated', icon: 'edit_note' },
    { feature: 'Maintenance', legacy: 'Manual updates every sprint', autotestify: 'AI Self-Healing', icon: 'build' },
    { feature: 'Redundancy Check', legacy: 'None', autotestify: 'Smart Deduplication', icon: 'filter_list' },
    { feature: 'Failure Prediction', legacy: 'Not available', autotestify: 'Deep ML Insights', icon: 'psychology' },
    { feature: 'Coverage Analysis', legacy: 'Manual review', autotestify: 'Automated AI Scoring', icon: 'analytics' },
    { feature: 'Re-test Strategy', legacy: 'Run all tests every time', autotestify: 'Impact-Based Targeting', icon: 'cycle' },
  ];

  testimonials = [
    {
      quote: 'AutoTestify cut our regression cycle from 4 hours to 40 minutes. The duplicate detection alone paid for itself in week one.',
      name: 'Sarah Chen',
      role: 'Lead QA Engineer',
      company: 'TechFlow Inc.',
      avatar: 'SC',
      color: 'primary',
    },
    {
      quote: "The AI failure prediction is eerily accurate. We get notified before a test even runs that it's likely to fail — game changer.",
      name: 'Marcus Rodriguez',
      role: 'CTO',
      company: 'Nexus Labs',
      avatar: 'MR',
      color: 'secondary',
    },
    {
      quote: "We reduced flaky tests by 94% in the first month. The smart re-test feature means we're only running what actually matters.",
      name: 'Aisha Patel',
      role: 'VP Engineering',
      company: 'ScaleUp Co.',
      avatar: 'AP',
      color: 'tertiary',
    },
  ];

  howItWorksSteps = [
    {
      icon: 'edit_note',
      color: 'primary',
      step: '01',
      title: 'Create',
      description: 'Write tests in plain English or record interactions. Our AI maps them to robust, maintainable selectors automatically.',
    },
    {
      icon: 'psychology',
      color: 'secondary',
      step: '02',
      title: 'Analyze',
      description: 'AutoTestify scans your app and entire test suite for bottlenecks, vulnerabilities, and optimization opportunities.',
    },
    {
      icon: 'play_arrow',
      color: 'tertiary',
      step: '03',
      title: 'Execute',
      description: 'Deploy tests globally with intelligent scheduling. AI predicts failures before they happen and self-heals broken tests.',
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
