import { useState, type InputHTMLAttributes, type ReactElement } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import { Icon } from './Icon';

type PasswordInputProps = Omit<InputHTMLAttributes<HTMLInputElement>, 'type'>;

/**
 * A password box with a button to show what was typed.
 *
 * On a tablet keyboard a mistyped password is easy and invisible, and the only way to
 * find it otherwise is to clear the box and start again. The text goes back to dots
 * whenever the screen is left, because the state lives here and nowhere else.
 *
 * Takes everything an input takes except its type, which this decides.
 */
export function PasswordInput({
  className,
  disabled,
  ...rest
}: PasswordInputProps): ReactElement {
  const { t } = useTranslation();
  const [visible, setVisible] = useState(false);

  return (
    <div className="relative">
      <input
        {...rest}
        type={visible ? 'text' : 'password'}
        disabled={disabled}
        className={[className ?? 'field-input', 'pe-12'].join(' ')}
      />
      <button
        type="button"
        className="absolute inset-y-0 end-0 grid w-12 place-items-center text-ink-muted transition-colors hover:text-ink disabled:opacity-50"
        aria-label={visible ? t('password.hide') : t('password.show')}
        aria-pressed={visible}
        disabled={disabled}
        onClick={() => {
          setVisible((current) => !current);
        }}
      >
        <Icon name={visible ? 'eyeOff' : 'eye'} className="size-5" />
      </button>
    </div>
  );
}
