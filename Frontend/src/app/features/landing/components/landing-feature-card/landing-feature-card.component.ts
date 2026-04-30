import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-landing-feature-card',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './landing-feature-card.component.html',
  styleUrl: './landing-feature-card.component.scss'
})
export class LandingFeatureCardComponent {
  @Input({ required: true }) icon = '';
  @Input() eyebrow = '';
  @Input({ required: true }) title = '';
  @Input({ required: true }) description = '';
  @Input() accent: 'primary' | 'secondary' | 'neutral' = 'primary';
}
